package main

import (
	"context"
	pb "github.com/dima1034/simple-replicated-log/Secondary/log_service/proto"
	"google.golang.org/grpc"
	"google.golang.org/grpc/grpclog"
	"log"
	"net"
	"time"
	"os"
)

type server struct {
	logs []string
	pb.UnimplementedLogServiceServer
}

func (s *server) AppendMessage(ctx context.Context, in *pb.MessageRequest) (*pb.MessageReply, error) {
	
	time.Sleep(2 * time.Second)

	s.logs = append(s.logs, in.Text)

	log.Printf("Appended message: %s", in.Text)
	
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
