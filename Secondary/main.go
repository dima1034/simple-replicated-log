package Secondary

import (
	"context"
	pb "github.com/dima1034/simple-replicated-log/Secondary/Protos"
	"google.golang.org/grpc"
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
	lis, err := net.Listen("tcp", ":5000")
	if err != nil {
		log.Fatalf("Failed to listen: %v", err)
	}
	s := grpc.NewServer()
	pb.RegisterLogServiceServer(s, &server{})
	if err := s.Serve(lis); err != nil {
		log.Fatalf("Failed to serve: %v", err)
	}
}
