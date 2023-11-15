protoc --go_out=./log-service --go_opt=paths=source_relative --go-grpc_out=./log-service --go-grpc_opt=paths=source_relative -I=$PWD $PWD/proto/log-service.proto
