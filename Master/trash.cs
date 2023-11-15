
// async Task<bool> TryReplicateMessageAsync(string message, string secondaryAddress, int maxRetries, TimeSpan delay)
// {
//     int retryCount = 0;
//     while (retryCount < maxRetries)
//     {
//         try
//         {
//             var channel = GrpcChannel.ForAddress(secondaryAddress);
//             var client = new Log.LogService.LogServiceClient(channel);
//             var reply = await client.AppendMessageAsync(new MessageRequest { Text = message, Id = sharedId });
//             
//             if (reply.Success)
//             {
//                 return true;
//             }
//         }
//         catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
//         {
//             // Log the exception
//             await Task.Delay(delay);
//             retryCount++;
//         }
//     }
//     return false;
// }
//
// async Task<MessageReply> AppendMessageWithWriteConcernAsync(string message, int writeConcern)
// {
//     var tasks = secondaryAddresses.Select(address => TryReplicateMessageAsync(message, address, 3, new TimeSpan(500))).ToList();
//     
//     // Wait for the tasks to complete according to the write concern
//     var completedTasks = await Task.WhenAll(tasks);
//     if (completedTasks.Count(success => success) >= writeConcern)
//     {
//         // Proceed with the replication
//     }
//     else
//     {
//         // Handle the case when the write concern is not met
//     }
// }


// var requiredAcks = w == secondaryAddresses.Count + 1 ? replicationTasks : replicationTasks.Take(w - 1);
// var ackTasks = await Task.WhenAll(requiredAcks);
// var ackCount = ackTasks.Count(success => success) + 1; // Including the master ACK

// if (ackCount >= w)
// {
//     logs.Add(sharedId, message); // Log the message on the master
//     cts.Cancel(); // Cancel all remaining tasks
//     logger.LogInformation($"Message '{message}' logged with {ackCount} ACKs.");
//     return Results.Accepted();
// }
// else
// {
//     // If not enough ACKs and the request is not canceled, return a problem
//     logger.LogError("Failed to achieve required write concern level.");
//     return Results.Problem("Failed to achieve required write concern level.");
// }
//     
// // Wait for the required number of acknowledgements
// while (ackCount < w && !httpContext.RequestAborted.IsCancellationRequested)
// {
//     var task = await Task.WhenAny(replicationTasks);
//         
//     replicationTasks.Remove(task);
//         
//     if (await task)
//     {
//         Interlocked.Increment(ref ackCount);
//     }
//
//     if (ackCount >= w)
//     {
//         break;
//     }
// }
//     
// // achieved, or if the request was cancelled
// if (ackCount < w && httpContext.RequestAborted.IsCancellationRequested)
// {
//     logger.LogError("Request cancelled before achieving required write concern level.");
//     return Results.Problem("Request cancelled before achieving required write concern level.");
// }
// else if (ackCount < w)
// {
//     logger.LogError("Failed to achieve required write concern level.");
//     return Results.Problem("Failed to achieve required write concern level.");
// }
//
// logger.LogInformation($"Message '{message}' logged with {ackCount} ACKs.");
// return Results.Accepted();




// app.MapPost("/log", (string message, int w) =>
// {
//     logs.Add(message);
//
//     int ackCount = 1; // Start with master ACK
//     List<Task> replicationTasks = new List<Task>();
//     string sharedId = Guid.NewGuid().ToString();
//     
//     foreach (var address in secondaryAddresses)
//     {
//         var channel = GrpcChannel.ForAddress(address);
//         var client = new Log.LogService.LogServiceClient(channel);
//
//         // Start replication asynchronously
//         replicationTasks.Add(Task.Run(() => 
//         {
//             var reply = client.AppendMessage(new MessageRequest { Text = message, Id = sharedId });
//             if (reply.Success)
//             {
//                 Interlocked.Increment(ref ackCount);
//             }
//         }));
//     }
//
//     Task.WaitAll(replicationTasks.Take(w - 1).ToArray()); // Wait only for 'w - 1' tasks (acknowledgments)
//
//     if (ackCount < w)
//     {
//         logger.LogError("Failed to get enough ACKs");
//         return Results.Problem("Failed to replicate message based on write concern");
//     }
//
//     logger.LogInformation($"Message '{message}' logged with {ackCount} ACKs");
//     return Results.Accepted();
// });

// async Task ReplicateMissedMessagesAsync(string secondaryAddress, string lastId)
// {
//     var missedMessages = logs
//         .Where(m => String.Compare(m.Key, lastId) > 0)
//         .ToList();
//
//     // Create a batch message request with all missed messages
//     var batchRequest = new BatchMessageRequest();
//     batchRequest.Messages
//         .AddRange(missedMessages.Select(m => new MessageRequest { Id = m.Key, Text = m.Value }));
//
//     var channel = GrpcChannel.ForAddress(secondaryAddress);
//     var client = new Log.LogService.LogServiceClient(channel);
//     
//     try
//     {
//         var reply = await client.BatchAppendMessagesAsync(batchRequest);
//         if (reply.Success)
//         {
//             // // Update the last message ID for this secondary
//             // KeyValuePair<string,string>? lastMessage = missedMessages.LastOrDefault();
//             // if (lastMessage != null)
//             // {
//             //     logs[secondaryAddress] = lastMessage;
//             // }
//         }
//     }
//     catch (RpcException ex)
//     {
//         logger.LogError($"Failed to replicate missed messages to {secondaryAddress}");
//         // Handle exception (e.g., retry logic)
//     }
// }
