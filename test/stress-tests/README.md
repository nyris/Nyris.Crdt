## Run Stress Test or Create Concurrent Requests

#### Usage:


1. Install [`k6`](https://k6.io/docs/getting-started/installation/)
2. Run

```bash
k6 run -u 100 --duration 30s .\script.js # 100 Concurrent Users for 30s
```

or

```bash
k6 run -u 100 -i 1000 .\script.js # 100 Concurrent Users with overall 1000 request divided among them
```