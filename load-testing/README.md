# Load testing for telemetry generation

[`customer-load-test.js`](customer-load-test.js) is a [k6](https://k6.io) script
that fires a randomized, weighted mix of GET/POST/PUT/DELETE calls at the Customer
API, including requests that are deliberately built to fail: bad payloads, missing
IDs, duplicate emails. The point is to give Tempo, Prometheus, and Loki something
varied and realistic to show off in Grafana. This isn't meant as a correctness
test, so the 400s, 404s, and 409s it triggers are expected, not bugs.

We picked k6 because it's built by Grafana Labs, a natural pairing for this stack,
and it runs just as well through Docker, which this project already needs anyway.
Nothing extra to install.

## Run it

With the app and the observability stack (`docker compose up -d`) both running:

```bash
docker run --rm -i \
  -e BASE_URL=http://host.docker.internal:5027 \
  -e VUS=20 -e DURATION=2m \
  grafana/k6 run - < load-testing/customer-load-test.js
```

`host.docker.internal` is how a container reaches the host's `localhost` on Mac and
Windows, right out of the box. **On Linux**, add
`--add-host=host.docker.internal:host-gateway` to the command above (needs Docker
Engine 20.10+) — without it, `host.docker.internal` won't resolve there at all.

The defaults are 20 virtual users for 2 minutes, which is comfortably thousands of
requests (an 8-VU, 15-second run during development produced ~29,000). Tune it
with the `VUS`, `DURATION`, and `BASE_URL` environment variables:

```bash
docker run --rm -i \
  -e BASE_URL=http://host.docker.internal:5027 \
  -e VUS=50 -e DURATION=5m \
  grafana/k6 run - < load-testing/customer-load-test.js
```

Keep `VUS` moderate rather than pushing it much higher: the app's SQLite database
serializes writers, so a large jump in concurrent VUs tends to surface as
`SQLITE_BUSY` write-lock errors instead of more throughput.

## What it generates

Each iteration picks from a weighted random mix of actions (see the `actions`
array in the script if you want to change the weights):

| Action | Endpoint | Expected result |
|---|---|---|
| List | `GET /customers` | 200 |
| Get (hit) | `GET /customers/{id}` | 200 or 404 |
| Get (miss) | `GET /customers/{id}` with a bogus id | 404 |
| Create | `POST /customers` | 201 |
| Create (invalid) | `POST /customers` with bad payload | 400 |
| Create (duplicate) | `POST /customers` reusing an existing email | 409 |
| Update (hit) | `PUT /customers/{id}` | 204 or 404 |
| Update (miss) | `PUT /customers/{id}` with a bogus id | 404 |
| Delete | `DELETE /customers/{id}` | 204 or 404 |

(One correction from how this is sometimes described elsewhere: a successful
update returns `204 No Content`, not `200` — `CustomersController.Update` returns
`NoContent()`, so that's the real expected code here.)

Each virtual user remembers the customers it's created (id *and* email), so the
get, update, delete, and duplicate-email calls have real records to target
instead of only ever hitting empty ones. The duplicate-email case is a genuine
409, not a fabricated status code — `Customer.Email` has a real unique index
(see the `AddUniqueEmailIndex` migration) and `CustomerService.CreateCustomerAsync`
checks for it before inserting. Note that an update changes a customer's email
in the database, so the script updates its own bookkeeping after a successful
update — otherwise the duplicate-email case would eventually try to reuse an
email that's no longer actually there, and get a surprising 201 instead.

A note on reading k6's own summary: its `http_req_failed` metric flags any 4xx or
5xx response as a "failure," and since we're generating those on purpose, that
number will sit somewhere around 15 to 30 percent quite legitimately. Look at the
`checks` section instead — those assert the specific status code each action
expects, and should sit close to 100 percent, aside from the odd bit of
nondeterminism where a "hit" action races a concurrent delete from another VU.

## Troubleshooting: everything comes back as a check failure

If every single check fails, including ones that should trivially pass like `list:
200`, k6 almost certainly isn't reaching this app at all — it's hitting something
else that happens to be listening on the same port. This isn't a flaw in Docker or
in `host.docker.internal` itself; it happens when something else on the host has
already bound `5027` (or one of its address families) before the app started, so
`host.docker.internal` resolves there but gets a response from the wrong process.

To check for this yourself:

```bash
lsof -nP -iTCP:5027 -sTCP:LISTEN     # look for more than one process on :5027 (Mac/Linux)
curl -4 http://127.0.0.1:5027/api/v1/customers   # force IPv4
curl -6 http://[::1]:5027/api/v1/customers        # force IPv6
```

If those two commands give you different results, something else is squatting on
the port. Find it in the `lsof` output above and stop it, or run the app on a
different port via `ASPNETCORE_URLS` and point `BASE_URL` at that instead.

## After running

Open Grafana at `localhost:3000` (no login — anonymous access is provisioned with
the Admin org role), go to Explore, and take a look around: Prometheus, where
`customers_operation_duration_milliseconds_bucket` carries real exemplars you can
click straight through to the originating trace in Tempo; Tempo, where searching
by service name shows the volume and mix of traces, including the error-status
ones (and where thinner-than-generated volumes of plain 200/201/204 traces are
`tail_sampling` doing its job — errors and slow traces are always kept, routine
successes are sampled down to 10%); and Loki, where filtering by `service_name`
shows structured `trace_id`/`span_id` metadata on every log line, with a "View
Trace" link straight to Tempo.

Traces and logs both have `customer.email`/`customer.phone_number` (or
`CustomerEmail`) redacted to a hash rather than left as plaintext — that's the
collector's `redaction` processor, not something this script does.
