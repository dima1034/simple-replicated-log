# Naive Distributed Log System

This project is a basic implementation of a distributed log system with tunable semi-synchronicity for replication.

## Prerequisites

Build and run the services:
```bash
docker-compose up --build --force-recreate
```

- By default, the master runs on port **5000**.
- By default, the secondaries runs on port **5300** and **5301** respectively, but you can change this with the PORT environment variable.

<br>
<br>

## Interacting with the Distributed Log System

To append a message to the log with a write concern of 1 (just the master):
```bash
curl -X POST "http://localhost:5000/log?message=YourMessageHere&w=1"
```
Replace **YourMessageHere** with the actual message you wish to send.
You can adjust the **w** parameter to specify the write concern (how many acknowledgements are needed before responding).

<br>
<br>

# Iteration 1
## Replicated Log Architecture:

### **Components**:
  - **Master**: Manages the main log and handles replication.
  - **Secondaries**: Any number can be added; they maintain replicated logs.

### **Master Server**:
  - **POST**: Adds a message to the in-memory list.
  - **GET**: Retrieves all messages in the list.

### **Secondary Server**:
  - **GET**: Retrieves all replicated messages.

### **Features**:
  - Messages are replicated to all Secondaries after each POST on Master.
  - Master waits for an acknowledgment (ACK) from every Secondary before concluding a POST.
  - Introduced a delay on a Secondary to simulate blocking replication.
  - Communication is assumed perfect (no lost messages or failures).

### **Tech Details**:
  - gRPC used for communication between Master and Secondaries.
  - Enabled logging.
  - Deployed Master and Secondaries in Docker containers. 

<br>
<br>

# Iteration 2
We need to perform the following tasks:

- Implement Write Concerns on the primary server.
- Introduce Artificial Delay for replicas to emulate inconsistency.
- Handle Messages Deduplication and total ordering on secondary nodes.

<br>
<br>

# Iteration 3
We need to perform the following tasks:

- Exactly-once 
- Heartbeats
- Quorum
- Deduplication
- Ordering
- Retry

<br>
<br>

# Changelog
- Drop docker.override, as it seems to be confusing
- Drop hardcoded urls from Master, and define them in appsettings
- Timestamp Handling in Secondary Server:<br>
Introduced handling for timestamp.Timestamp in the secondary server's Message struct.
Modified the AppendMessage and BatchAppendMessages methods to convert timestamp.Timestamp to Go's time.Time for correct timestamp handling.
Adjusted logging to format the Timestamp field correctly.
HTTP Endpoint for Logs in Secondary Server:

- Added an HTTP server running alongside the gRPC server in the secondary server.<br>
Implemented a new HTTP handler (handleGetLogs) to return all logged messages as JSON.
Configured the HTTP server to listen on a separate port (default 8080 or as specified by the HTTP_PORT environment variable).
Fixing Instance Sharing Between gRPC and HTTP Servers:

- Corrected the instance sharing issue between gRPC and HTTP servers in the secondary server.<br>
Ensured that both gRPC and HTTP servers share the same instance of the server struct, allowing them to manage and return the same set of logs.
Minor Corrections and Improvements:

- Made various minor code corrections and improvements, including adjusting logging statements and refining the implementation of server functions for better performance and readability.