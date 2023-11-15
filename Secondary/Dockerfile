FROM golang:1.21 as builder

# Install the protocol buffer compiler
RUN apt-get update && apt-get install -y protobuf-compiler

# Install Go plugins for protocol buffers
RUN go install google.golang.org/protobuf/cmd/protoc-gen-go@v1.26.0
RUN go install google.golang.org/grpc/cmd/protoc-gen-go-grpc@v1.1.0

# Set the Go binary path for the installed plugins
ENV PATH="$PATH:$(go env GOPATH)/bin"

WORKDIR /src
COPY go.mod go.sum ./
RUN go mod download
COPY . .

# Run the protoc command
RUN protoc --go_out=./log-service --go_opt=paths=source_relative --go-grpc_out=./log-service --go-grpc_opt=paths=source_relative -I=$PWD $PWD/proto/log-service.proto

# Build the Go binary
RUN CGO_ENABLED=0 GOOS=linux go build -o /app/secondary ./main.go

FROM alpine:latest
WORKDIR /app
COPY --from=builder /app/secondary /app/secondary
ENTRYPOINT ["/app/secondary"]
