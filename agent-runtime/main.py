"""
Advertified Agent Runtime
Main entry point for the FastAPI application with Amazon Bedrock AgentCore integration
"""

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
import structlog
from contextlib import asynccontextmanager
import os
from dotenv import load_dotenv

# Load environment variables
load_dotenv()

# Configure structured logging
structlog.configure(
    processors=[
        structlog.contextvars.merge_contextvars,
        structlog.processors.add_log_level,
        structlog.processors.JSONRenderer()
    ]
)

logger = structlog.get_logger()


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan manager"""
    logger.info("Starting Advertified Agent Runtime")
    # Startup logic here
    yield
    # Shutdown logic here
    logger.info("Shutting down Advertified Agent Runtime")


# Create FastAPI application
app = FastAPI(
    title="Advertified Agent Runtime",
    description="AI agent orchestration service using Amazon Bedrock AgentCore",
    version="1.0.0",
    lifespan=lifespan
)

# Configure CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=os.getenv("ALLOWED_ORIGINS", "http://localhost:5173").split(","),
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/health")
async def health_check():
    """Health check endpoint"""
    return {
        "status": "healthy",
        "service": "advertified-agent-runtime",
        "version": "1.0.0"
    }


@app.get("/")
async def root():
    """Root endpoint"""
    return {
        "service": "Advertified Agent Runtime",
        "status": "running",
        "agents": "11 specialized agents for marketing intelligence"
    }


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(
        "main:app",
        host="0.0.0.0",
        port=int(os.getenv("AGENT_RUNTIME_PORT", 8000)),
        reload=True if os.getenv("AGENT_RUNTIME_ENVIRONMENT") == "development" else False
    )