# Logging

The services only write their logs to stdout. A Fluentd container runs next to them as a sidecar, collects those logs
and forwards them. Here it prints them on its own stdout, which stands in for a central log system.

So no service has code or packages for shipping logs. Changing where the logs go only changes
[fluentd/fluent.conf](../fluentd/fluent.conf).

## How it works

1. Docker's `json-file` driver stores each container's stdout as JSON lines in `/var/lib/docker/containers`.
2. The services in `compose.yaml` use the `service-logging` settings, which add the container name to every line
   (`tag: "{{.Name}}"`).
3. Fluentd mounts that folder read-only and tails the files. It keeps only lines with a container name, so other
   containers on the machine and Fluentd's own output are left out.
4. Each record becomes `{"container": ..., "stream": ..., "log": ...}` and is printed.

Fluentd runs as root, because Docker's log files can only be read by root.

## See the logs

```bash
docker compose logs -f fluentd
```

Only one service:

```bash
docker compose logs --no-log-prefix fluentd | grep '"container":"bizcord-workspace-api-1"'
```

## Tested on

Docker Desktop for Windows (WSL 2). There the `/var/lib/docker/containers` path is the folder inside Docker's Linux VM,
so the mount works the same as on Linux.
