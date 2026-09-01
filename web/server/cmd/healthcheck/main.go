package main

import (
	"fmt"
	"net/http"
	"os"
	"time"
)

const expectedStatus = http.StatusOK

func main() {
	if len(os.Args) != 2 {
		fmt.Fprintln(os.Stderr, "usage: healthcheck <url>")
		os.Exit(2)
	}

	client := http.Client{Timeout: 2 * time.Second}
	response, err := client.Get(os.Args[1])
	if err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
	defer response.Body.Close()

	if response.StatusCode != expectedStatus {
		fmt.Fprintf(os.Stderr, "unexpected status: %d\n", response.StatusCode)
		os.Exit(1)
	}
}
