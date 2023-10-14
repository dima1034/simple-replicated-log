package main

import (
	"context"
	pb "github.com/dima1034/simple-replicated-log/Secondary/log_service/proto"
	"google.golang.org/grpc"
	"google.golang.org/grpc/grpclog"
	"log"
	"net"
	"os"
	"sort"
	"strconv"
	"time"
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
		delay = 0
	}
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
		Timestamp: time.Now(),
	}

	s.logs = append(s.logs, newMessage)
	s.orderLogs()

	log.Printf("Appended message: %s\n", in.Text)

	for _, msg := range s.logs {
		log.Printf("ID: %s, Text: %s, Timestamp: %s\n", msg.ID, msg.Text, msg.Timestamp.Format("2006-01-02 15:04:05"))
	}

	return &pb.MessageReply{Success: true}, nil
}

func main() {
	port := os.Getenv("PORT")
	if port == "" {
		port = "5300"
	}
	log.Printf("Starting server on port %s", port) // Logging the port the server is starting on

	listener, err := net.Listen("tcp", ":"+port)
	if err != nil {
		grpclog.Fatalf("failed to listen: %v", err)
	}

	opts := []grpc.ServerOption{}
	grpcServer := grpc.NewServer(opts...)

	pb.RegisterLogServiceServer(grpcServer, &server{})
	log.Println("Server registered and ready to accept connections.") // Logging server readiness

	if err := grpcServer.Serve(listener); err != nil {
		log.Fatalf("Failed to serve: %v", err)
	}
}
