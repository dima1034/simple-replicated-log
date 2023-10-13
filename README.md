# Distributed Log System

This project is a basic implementation of a distributed log system with tunable semi-synchronicity for replication.

## Prerequisites

Build and run the services:
```bash
docker-compose up --build
```

- By default, the master runs on port **5000**.
- By default, the secondaries runs on port **5300** and **5301** respectively, but you can change this with the PORT environment variable.

<br>
<br>

## Interacting with the Distributed Log System

To append a message to the log with a write concern of 1 (just the master):
```bash
curl -X POST "http://localhost:5000/log?message=helloWorldMaster&w=1"
```
Replace YourMessageHere with the actual message you wish to send.
You can adjust the w parameter to specify the write concern (how many acknowledgements are needed before responding).

## Iteration 1


## Iteration 2
We need to perform the following tasks:

Implement Write Concerns on the primary server.
Introduce Artificial Delay for replicas to emulate inconsistency.
Handle Messages Deduplication and total ordering on secondary nodes.