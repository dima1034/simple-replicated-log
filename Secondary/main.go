package main

import (
	"context"
	"encoding/json"
	"log"
	"net"
	"net/http"
	"os"
	"sort"
	"strconv"
	"time"

	pb "github.com/dima1034/simple-replicated-log/Secondary/log-service/proto"
	"google.golang.org/grpc"
	"google.golang.org/grpc/grpclog"
)

type server struct {
	logs []Message
	pb.UnimplementedLogServiceServer
}

type Message struct {
	ID        string
	Text      string
	Timestamp time.Time
}

func introduceDelay() {
	delayStr := os.Getenv("DELAY")
	delay, err := strconv.Atoi(delayStr)
	if err != nil || delay < 0 {
		return
	}

	log.Printf("Introducing delay of %d seconds\n", delay)
	time.Sleep(time.Duration(delay) * time.Second)
}

func introduceStartupDelay() {
	delayStr := os.Getenv("STARTUP_DELAY")
	delay, err := strconv.Atoi(delayStr)
	if err != nil || delay < 0 {
		return
	}

	log.Printf("Introducing delay of %d seconds\n", delay)
	time.Sleep(time.Duration(delay) * time.Second)
}

func (s *server) isDuplicate(id string) bool {
	for _, msg := range s.logs {
		if msg.ID == id {
			return true
		}
	}
	return false
}

func (s *server) orderLogs() {
	sort.SliceStable(s.logs, func(i, j int) bool {
		return s.logs[i].Timestamp.Before(s.logs[j].Timestamp)
	})
}

func (s *server) AppendMessage(ctx context.Context, in *pb.MessageRequest) (*pb.MessageReply, error) {

	introduceDelay()

	if s.isDuplicate(in.Id) {
		return &pb.MessageReply{Success: true}, nil
	}

	newMessage := Message{
		ID:        in.Id,
		Text:      in.Text,
		Timestamp: in.Timestamp.AsTime(),
	}

	s.logs = append(s.logs, newMessage)
	s.orderLogs()

	for _, msg := range s.logs {
		log.Printf("Appended message: %s, Timestamp: %s\n", msg.Text, msg.Timestamp.Format("2006-01-02 15:04:05"))
	}

	return &pb.MessageReply{Success: true}, nil
}

func (s *server) GetLastMessageID(ctx context.Context, in *pb.Empty) (*pb.LastMessageIDReply, error) {
	var lastID string
	if len(s.logs) > 0 {
		lastID = s.logs[len(s.logs)-1].ID
	}
	return &pb.LastMessageIDReply{Id: lastID}, nil
}

func (s *server) BatchAppendMessages(ctx context.Context, in *pb.BatchMessageRequest) (*pb.MessageReply, error) {
	for _, msg := range in.Messages {
		if !s.isDuplicate(msg.Id) {
			newMessage := Message{
				ID:        msg.Id,
				Text:      msg.Text,
				Timestamp: msg.Timestamp.AsTime(),
			}
			s.logs = append(s.logs, newMessage)
		}
	}
	s.orderLogs()

	log.Printf("Appended %d messages\n", len(in.Messages))

	for _, msg := range s.logs {
		log.Printf("Appended message: %s, Timestamp: %s\n", msg.Text, msg.Timestamp.Format("2006-01-02 15:04:05"))
	}

	return &pb.MessageReply{Success: true}, nil
}

// HTTP handler to return logs
func (s *server) handleGetLogs(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	if err := json.NewEncoder(w).Encode(s.logs); err != nil {
		http.Error(w, err.Error(), http.StatusInternalServerError)
	}
}

func main() {

	introduceStartupDelay()

	port := os.Getenv("PORT")
	if port == "" {
		port = "5300"
	}
	log.Printf("Starting server on port %s", port) // Logging the port the server is starting on

	listener, err := net.Listen("tcp", ":"+port)
	if err != nil {
		grpclog.Fatalf("failed to listen: %v", err)
	}

	// Create a new instance of the server struct
	srv := &server{}

	opts := []grpc.ServerOption{}
	grpcServer := grpc.NewServer(opts...)

	// Correctly use 'srv' while registering the gRPC server
	pb.RegisterLogServiceServer(grpcServer, srv)

	// Start the gRPC server in a separate goroutine
	go func() {
		log.Println("gRPC server registered and ready to accept connections.")
		if err := grpcServer.Serve(listener); err != nil {
			log.Fatalf("Failed to serve gRPC: %v", err)
		}
	}()

	// Setup HTTP server for logs using the same 'srv' instance
	http.HandleFunc("/log", srv.handleGetLogs)

	httpPort := os.Getenv("HTTP_PORT")
	if httpPort == "" {
		httpPort = "8080"
	}

	log.Printf("HTTP server starting on port %s", httpPort)
	if err := http.ListenAndServe(":"+httpPort, nil); err != nil {
		log.Fatalf("Failed to serve HTTP: %v", err)
	}
}
