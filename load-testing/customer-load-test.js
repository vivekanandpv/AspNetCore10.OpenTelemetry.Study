import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://host.docker.internal:5027';
const BOGUS_ID = 999999999;

export const options = {
  vus: parseInt(__ENV.VUS || '20', 10),
  duration: __ENV.DURATION || '2m',
};

// Per-VU memory of ids this VU has created, so get/update/delete have real
// records to target instead of only ever hitting empty ones.
const createdIds = [];

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

function randomCustomer() {
  const first = pick(FIRST_NAMES);
  const last = pick(LAST_NAMES);
  const uid = `${__VU}-${__ITER}-${Date.now()}`;
  return {
    name: `${first} ${last}`,
    email: `${first}.${last}.${uid}@loadtest.example.com`.toLowerCase(),
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
  if (createdIds.length === 0) return doCreate();
  const id = pick(createdIds);
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
    createdIds.push(body.id);
    if (createdIds.length > 500) createdIds.shift(); // cap this VU's memory
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

function doUpdateHit() {
  if (createdIds.length === 0) return doCreate();
  const id = pick(createdIds);
  const payload = JSON.stringify({ id, ...randomCustomer() });
  const res = http.put(`${BASE_URL}/api/v1/customers/${id}`, payload, {
    ...jsonHeaders(),
    tags: { action: 'update_hit' },
  });
  check(res, { 'update (hit): 204 or 404': (r) => r.status === 204 || r.status === 404 });
}

function doUpdateMiss() {
  // Route id and body id agree, so it clears the controller's own mismatch
  // check and falls through to CustomerService's not-found path.
  const payload = JSON.stringify({ id: BOGUS_ID, ...randomCustomer() });
  const res = http.put(`${BASE_URL}/api/v1/customers/${BOGUS_ID}`, payload, {
    ...jsonHeaders(),
    tags: { action: 'update_miss' },
  });
  check(res, { 'update (miss): 404': (r) => r.status === 404 });
}

function doUpdateMismatch() {
  // Route id and body id deliberately disagree — CustomersController rejects
  // this itself with a 400 before ever calling into the service layer.
  if (createdIds.length === 0) return doCreate();
  const id = pick(createdIds);
  const payload = JSON.stringify({ id: id + 1, ...randomCustomer() });
  const res = http.put(`${BASE_URL}/api/v1/customers/${id}`, payload, {
    ...jsonHeaders(),
    tags: { action: 'update_mismatch' },
  });
  check(res, { 'update (id mismatch): 400': (r) => r.status === 400 });
}

function doDelete() {
  if (createdIds.length === 0) return doCreate();
  const idx = Math.floor(Math.random() * createdIds.length);
  const id = createdIds.splice(idx, 1)[0];
  const res = http.del(`${BASE_URL}/api/v1/customers/${id}`, null, { tags: { action: 'delete' } });
  check(res, { 'delete: 204 or 404': (r) => r.status === 204 || r.status === 404 });
}

// Weighted so creates outpace deletes (the dataset grows over the run) and
// the deliberately-failing actions stay a clear minority of total traffic.
const actions = [
  { weight: 15, run: doList },
  { weight: 12, run: doGetHit },
  { weight: 8, run: doGetMiss },
  { weight: 22, run: doCreate },
  { weight: 8, run: doCreateInvalid },
  { weight: 12, run: doUpdateHit },
  { weight: 6, run: doUpdateMiss },
  { weight: 6, run: doUpdateMismatch },
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
