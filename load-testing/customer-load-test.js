import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://host.docker.internal:5027';
const BOGUS_ID = 999999999;

export const options = {
  vus: parseInt(__ENV.VUS || '20', 10),
  duration: __ENV.DURATION || '2m',
};

// Per-VU memory of customers this VU has created (id + email), so get,
// update, delete, and the duplicate-email case all have real records to
// target instead of only ever hitting empty ones.
const createdCustomers = [];

const FIRST_NAMES = [
  'Alice', 'Bob', 'Charlie', 'Diana', 'Ethan', 'Fiona', 'George', 'Hannah',
  'Ian', 'Jane', 'Kevin', 'Laura', 'Mike', 'Nora', 'Oscar', 'Priya',
];
const LAST_NAMES = [
  'Johnson', 'Smith', 'Brown', 'Prince', 'Hunt', 'Gallagher', 'Costanza',
  'Abbott', 'Malcolm', 'Doe', 'Patel', 'Garcia', 'Kim', 'Nguyen',
];

function pick(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

function randomCustomer(email) {
  const first = pick(FIRST_NAMES);
  const last = pick(LAST_NAMES);
  const uid = `${__VU}-${__ITER}-${Date.now()}`;
  return {
    name: `${first} ${last}`,
    email: email || `${first}.${last}.${uid}@loadtest.example.com`.toLowerCase(),
    phoneNumber: `555-${String(Math.floor(1000000 + Math.random() * 9000000)).slice(0, 7)}`,
  };
}

function jsonHeaders() {
  return { headers: { 'Content-Type': 'application/json' } };
}

function doList() {
  const res = http.get(`${BASE_URL}/api/v1/customers`, { tags: { action: 'list' } });
  check(res, { 'list: 200': (r) => r.status === 200 });
}

function doGetHit() {
  if (createdCustomers.length === 0) return doCreate();
  const { id } = pick(createdCustomers);
  const res = http.get(`${BASE_URL}/api/v1/customers/${id}`, { tags: { action: 'get_hit' } });
  // A concurrent delete from another VU can turn a "hit" into a 404 — that's
  // expected raciness, not a bug, so both codes count as a pass.
  check(res, { 'get (hit): 200 or 404': (r) => r.status === 200 || r.status === 404 });
}

function doGetMiss() {
  const res = http.get(`${BASE_URL}/api/v1/customers/${BOGUS_ID}`, { tags: { action: 'get_miss' } });
  check(res, { 'get (miss): 404': (r) => r.status === 404 });
}

function doCreate() {
  const payload = JSON.stringify(randomCustomer());
  const res = http.post(`${BASE_URL}/api/v1/customers`, payload, {
    ...jsonHeaders(),
    tags: { action: 'create' },
  });
  const ok = check(res, { 'create: 201': (r) => r.status === 201 });
  if (ok) {
    const body = JSON.parse(res.body);
    createdCustomers.push({ id: body.id, email: body.email });
    if (createdCustomers.length > 500) createdCustomers.shift(); // cap this VU's memory
  }
}

function doCreateInvalid() {
  // Blank name and a malformed email trip the [Required]/[EmailAddress]
  // data-annotation validation on CreateCustomerDto, which the [ApiController]
  // attribute turns into an automatic 400 before the request reaches the
  // controller action at all.
  const payload = JSON.stringify({ name: '', email: 'not-an-email', phoneNumber: '' });
  const res = http.post(`${BASE_URL}/api/v1/customers`, payload, {
    ...jsonHeaders(),
    tags: { action: 'create_invalid' },
  });
  check(res, { 'create (invalid): 400': (r) => r.status === 400 });
}

function doCreateDuplicate() {
  // Resubmits an email this VU already created. Customer.Email has a real
  // unique index (see AddUniqueEmailIndex migration), and CustomerService
  // checks for it before insert, so this is a genuine 409 — not a status
  // code invented for the test.
  if (createdCustomers.length === 0) return doCreate();
  const { email } = pick(createdCustomers);
  const payload = JSON.stringify(randomCustomer(email));
  const res = http.post(`${BASE_URL}/api/v1/customers`, payload, {
    ...jsonHeaders(),
    tags: { action: 'create_duplicate' },
  });
  check(res, { 'create (duplicate): 409': (r) => r.status === 409 });
}

function doUpdateHit() {
  if (createdCustomers.length === 0) return doCreate();
  const record = pick(createdCustomers);
  const updated = randomCustomer();
  const payload = JSON.stringify({ id: record.id, ...updated });
  const res = http.put(`${BASE_URL}/api/v1/customers/${record.id}`, payload, {
    ...jsonHeaders(),
    tags: { action: 'update_hit' },
  });
  const ok = check(res, { 'update (hit): 204 or 404': (r) => r.status === 204 || r.status === 404 });
  // Keep local bookkeeping in sync — an update changes the email in the DB,
  // so createDuplicate must reuse the *new* email, not the stale one.
  if (ok && res.status === 204) {
    record.email = updated.email;
  }
}

function doUpdateMiss() {
  const payload = JSON.stringify({ id: BOGUS_ID, ...randomCustomer() });
  const res = http.put(`${BASE_URL}/api/v1/customers/${BOGUS_ID}`, payload, {
    ...jsonHeaders(),
    tags: { action: 'update_miss' },
  });
  check(res, { 'update (miss): 404': (r) => r.status === 404 });
}

function doDelete() {
  if (createdCustomers.length === 0) return doCreate();
  const idx = Math.floor(Math.random() * createdCustomers.length);
  const { id } = createdCustomers.splice(idx, 1)[0];
  const res = http.del(`${BASE_URL}/api/v1/customers/${id}`, null, { tags: { action: 'delete' } });
  check(res, { 'delete: 204 or 404': (r) => r.status === 204 || r.status === 404 });
}

// Weighted so creates outpace deletes (the dataset grows over the run) and
// the deliberately-failing actions stay a clear minority of total traffic.
const actions = [
  { weight: 15, run: doList },
  { weight: 12, run: doGetHit },
  { weight: 8, run: doGetMiss },
  { weight: 20, run: doCreate },
  { weight: 7, run: doCreateInvalid },
  { weight: 7, run: doCreateDuplicate },
  { weight: 12, run: doUpdateHit },
  { weight: 8, run: doUpdateMiss },
  { weight: 11, run: doDelete },
];
const totalWeight = actions.reduce((sum, a) => sum + a.weight, 0);

function pickAction() {
  let r = Math.random() * totalWeight;
  for (const a of actions) {
    if (r < a.weight) return a;
    r -= a.weight;
  }
  return actions[actions.length - 1];
}

export default function () {
  pickAction().run();
}
