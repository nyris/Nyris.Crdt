```shell
kubectl -ndistributed-prototype-test create secret generic distributed-prototype-test-sample-app-config-v0.1.0 --from-file=appsettings.json=appsettings.integration.json --dry-run=client -o yaml | kubectl -ndistributed-prototype-test replace secret distributed-prototype-test-sample-app-config-v0.1.0 -f - 
```