# Load testing for telemetry generation

[`customer-load-test.js`](customer-load-test.js) is a [k6](https://k6.io) script
that fires a randomized, weighted mix of GET/POST/PUT/DELETE calls at the Customer
API, including requests that are deliberately built to fail: bad payloads, missing
IDs, mismatched IDs. The point is to give SigNoz something varied and realistic to
show off — traces, logs, and metrics across a real mix of status codes, not just a
wall of 200s. This isn't meant as a correctness test, so the 400s and 404s it
triggers are expected, not bugs.

We picked k6 because it scripts this kind of weighted, stateful traffic cleanly in
plain JS, and it runs straight through Docker with nothing to install — you already
need Docker for the SigNoz stack, so this adds no new tooling.

## Run it

With the app and the SigNoz stack (`docker compose up -d`) both running:

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
requests (a 5-VU, 15-second test run during development produced over 28,000). Tune
it with the `VUS`, `DURATION`, and `BASE_URL` environment variables:

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
| Create (invalid) | `POST /customers` with a blank name and malformed email | 400 |
| Update (hit) | `PUT /customers/{id}` | 204 or 404 |
| Update (miss) | `PUT /customers/{id}` with a bogus id | 404 |
| Update (id mismatch) | `PUT /customers/{id}` where the body's id disagrees with the route | 400 |
| Delete | `DELETE /customers/{id}` | 204 or 404 |

Each virtual user remembers the ids it's created, so the get, update, and delete
calls have real records to target instead of only ever hitting empty ones.

There's no "duplicate email" case: `Customer.Email` has no uniqueness constraint in
this app (no `HasIndex().IsUnique()` on the EF Core model, no duplicate check in
`CustomerService`), so a repeated email would just succeed with 201, not fail. The
id-mismatch case takes its place — `CustomersController.Update` checks the route id
against the body's id itself and returns 400 before the service layer ever sees
the request, which is a genuinely distinct failure path worth exercising.

A note on reading k6's own summary: its `http_req_failed` metric flags any 4xx or
5xx response as a "failure," and since we're generating those on purpose, that
number will sit close to the combined weight of the four always-fail actions above
(get miss, create invalid, update miss, update id-mismatch) — around a quarter of
total requests with the default weights. Look at the `checks` section instead —
those assert the specific status code each action expects, and should sit at (or
very close to) 100%, aside from the odd bit of nondeterminism where a "hit" action
races a concurrent delete from another VU.

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

If those two commands give different results, something else is squatting on the
port — find it in the `lsof` output and stop it, or run the app on a different port
via `ASPNETCORE_URLS` and point `BASE_URL` at that instead.

## After running

Open SigNoz at `localhost:8080` and look around:

- **Traces** — search by service name `AspNetCore10.OpenTelemetry.Study` to see
  the volume and mix of spans, including the 400/404 ones; filter by
  `http.response.status_code` to see the spread this run produced.
- **Logs** — filter by `service.name`, then jump from a log line to its trace via
  the trace id SigNoz already correlates for you.
- **Metrics** — `customers.created`, `customers.deleted`, and
  `customers.operation.duration` all pick up real variance once there's this much
  traffic behind them. The app attaches exemplars (trace id + span id) to the
  histogram's data points, but self-hosted SigNoz doesn't currently store or
  surface them anywhere in the UI — there's no `exemplar` column in its ClickHouse
  metrics schema, so the collector drops them before they'd ever show up here. To
  correlate a slow operation with its trace, use the **Traces** tab instead and
  filter/sort by duration.
