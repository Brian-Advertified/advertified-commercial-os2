# ADR-0001: Use Amazon Bedrock AgentCore for AI Agent Orchestration

## Status
Accepted

## Context
The Advertified Unified system requires orchestration of 11 specialized AI agents for marketing intelligence and campaign management. We need a production-ready, secure, and scalable platform for:

- Agent dispatch and health management
- Tool authorization and policy enforcement  
- Cost tracking and budget controls
- Memory management and session isolation
- Integration with multiple model providers

The specification explicitly calls for a Python/FastAPI AgentCore-compatible service with typed schemas.

## Decision
We will use Amazon Bedrock AgentCore as the primary AI agent orchestration platform.

### Rationale
1. **Framework Agnostic**: Supports LangChain, OpenAI Agents SDK, Claude Agent SDK, Strands SDK, and custom frameworks
2. **Model Agnostic**: Works with Amazon Bedrock, OpenAI, Google Gemini, and OpenAI-compatible providers
3. **Built-in Security**: Authentication, access control, and governance at the platform layer
4. **No Infrastructure Management**: Serverless deployment with AWS infrastructure
5. **Production Ready**: Debugging, optimization tools, and monitoring capabilities
6. **Specification Alignment**: Directly meets the AgentCore compatibility requirement in Section 17

### Alternatives Considered
- **Custom Python orchestration**: Would require building security, scaling, and monitoring from scratch
- **LangChain only**: Tightly coupled to one framework, less security controls
- **OpenAI Agents SDK**: Limited to OpenAI models, less framework flexibility

## Consequences

### Positive
- Faster time-to-market with managed infrastructure
- Built-in security and compliance controls
- Flexibility to change frameworks and models without rewriting orchestration
- Production-grade monitoring and debugging tools
- Meets specification requirements exactly

### Negative
- AWS dependency for core AI infrastructure
- Learning curve for AgentCore-specific patterns
- Potential vendor lock-in for orchestration layer

### Risks and Mitigations
- **Risk**: AWS service outage could block AI operations
  - **Mitigation**: Implement fallback to deterministic provider and cached results
- **Risk**: Cost overruns from AI usage
  - **Mitigation**: Implement per-run budget caps and cost tracking with alerts
- **Risk**: Vendor lock-in
  - **Mitigation**: Use provider-neutral interfaces where possible, maintain ability to switch

## Implementation
1. Set up Python/FastAPI AgentCore-compatible runtime service
2. Implement the 11 specialized agents using AgentCore patterns
3. Configure tool authorization and policy enforcement
4. Implement cost tracking and budget controls
5. Set up deterministic provider for testing
6. Configure monitoring and alerting
7. Integrate with Commercial API through typed contracts

## References
- Advertified Unified Specification v1.1, Sections 6, 16, 17, 22
- Amazon Bedrock AgentCore Documentation: https://docs.aws.amazon.com/bedrock-agentcore/
- Agent Integration specification (Section 22)

## Participants
- System Architect - Technical leadership
- AI Engineer - Agent implementation  
- DevOps Engineer - Infrastructure setup

## Date
2026-08-29