package main

import (
	"context"
	pb "github.com/dima1034/simple-replicated-log/Secondary/log_service/proto"
	"google.golang.org/grpc"
	"google.golang.org/grpc/grpclog"
	"log"
	"net"
	"time"
)

type server struct {
	logs []string
	pb.UnimplementedLogServiceServer
}

func (s *server) AppendMessage(ctx context.Context, in *pb.MessageRequest) (*pb.MessageReply, error) {
	time.Sleep(2 * time.Second) // Introduce delay
	s.logs = append(s.logs, in.Text)
	return &pb.MessageReply{Success: true}, nil
}

func main() {
	listener, err := net.Listen("tcp", ":5300")
	if err != nil {
		grpclog.Fatalf("failed to listen: %v", err)
	}

	opts := []grpc.ServerOption{}
	grpcServer := grpc.NewServer(opts...)

	pb.RegisterLogServiceServer(grpcServer, &server{})

	if err := grpcServer.Serve(listener); err != nil {
		log.Fatalf("Failed to serve: %v", err)
	}
}
