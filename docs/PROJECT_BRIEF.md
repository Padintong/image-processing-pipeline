# Project Brief: Image Processing Pipeline

## Summary

A portfolio project demonstrating cloud-native backend architecture through a real-world image processing pipeline for game studios and digital asset workflows. The system accepts image assets, processes them through a queue-based worker architecture, and produces web-optimized outputs and achievement artwork. Built to learn and demonstrate: .NET Web API, Docker, Kubernetes, PostgreSQL, multi-agent Claude architecture, Cloudflare R2, REST API design, and CI/CD patterns.

## Background and Motivation

Carlos Padilla is a Game Developer and Software Engineer with extensive C# and Unity experience, database design background, and real prior work building an image processing pipeline at Hugintech (achievement artwork generation, multi-size web exports). This project formalizes and expands that experience into a cloud-native architecture using technologies frequently required in current job listings: Azure, AWS/cloud storage, .NET, Docker, Kubernetes, REST APIs.

## Core Features

**Single mode** — submit one image via REST API, receive processed outputs. Good for interactive use and testing.

**Batch mode** — point the system at a folder or manifest, it processes all assets missing their outputs. Designed to work within a client's existing project folder structure, only processing what's new or changed.

**Processing capabilities:**

- Resize images to multiple output dimensions for web use
- Generate achievement artwork by overlapping configurable star ratings (1–6, color and B/W, two sizes) onto exercise/game asset images
- Format conversion (PNG → WebP, AVIF)
- Metadata extraction (dimensions, file size, dominant colors) stored in PostgreSQL
- Image validation before processing (minimum resolution, aspect ratio, transparency rules)
- Asset diff detection — batch mode only reprocesses changed or new files
- Processing history and audit trail exposed via REST API

## Architecture

**Entry point** — .NET Web API (RESTful), OpenAPI/Swagger spec auto-generated. Accepts single and batch job submissions.

**Queue** — message queue decoupling API from workers. Jobs submitted to queue, workers pull independently.

**Workers** — .NET services running in Docker containers, deployed to Kubernetes. Each worker processes one job at a time. Multiple worker instances run in parallel.

**Claude agents inside workers:**

- Validation agent — checks submitted images against configurable rules before processing begins
- Analysis agent — monitors batch jobs for anomalies, flags unexpected patterns, decides processing priority

**Storage:**

- Cloudflare R2 — processed image outputs (S3-compatible API, zero egress fees)
- PostgreSQL via Supabase — job history, asset metadata, audit trail, processing results

**Infrastructure:**

- Docker — all services containerized
- Kubernetes — local via k3s for development and portfolio demonstration
- GitHub Actions — automated testing pipeline
- Cloudflare R2 — asset storage

## Tech Stack Summary

- .NET 10 Web API (C#)
- Docker
- Kubernetes (k3s locally)
- PostgreSQL (Supabase free tier)
- Cloudflare R2
- Claude API (multi-agent, at least two agents)
- GitHub + GitHub Actions
- Terraform (infrastructure as code, introduced progressively)

## Assets and Data

- Images rendered in Blender by Carlos using personal models (owned outright, no IP concerns)
- Synthetic batch manifests generated programmatically for testing
- No real user data, no sensitive data, no HuginTech assets

## Tools and Documentation

- **Mermaid** — architecture and flow diagrams living in the GitHub README, version-controlled with code
- **Notion** — project documentation, Architecture Decision Records (ADRs), learning log
- **Excalidraw** — connected via MCP; for sketching ideas still taking shape, before they're formalized into Mermaid diagrams
- **GitHub** — public repository, source of truth for code
- **Claude Code** — scaffolding, Dockerfiles, Kubernetes manifests
- **Cowork** — file management, cross-session continuity, guided build process
- **Visual Studio 2026** — primary local IDE; required for full .NET 10 tooling (VS 2022 has no .NET 10 project/debugger support)
- **VS Code** — secondary editor for everything outside the .NET solution: Mermaid preview, Kubernetes manifests, Dockerfiles, Terraform, GitHub Actions workflows

## Cost

Target: zero. All tools and services used are on free tiers. Potential future experiment with Azure Container Apps using $200 new account credit if cloud Kubernetes experience is wanted for CV. Decision postponed.

## Learning Goals

- Understand and be able to explain: REST API design, Docker containerization, Kubernetes concepts, message queue patterns, multi-agent architecture, cloud storage, CI/CD
- Each concept introduced with explanation of what it is, why it exists, and how it fits the bigger picture
- Architecture decisions documented as ADRs, written as if justifying to a team
- Claude plays devil's advocate on architectural choices
- Living learning log maintained separately from technical documentation throughout the build

## Future Extensions

- CI/CD pipeline monitor (Option C from planning) — reuses ~70% of this infrastructure
- Game session analytics pipeline (Option A variation) — same architecture, different domain
- UI layer via Claude Design — asset library browser, job queue dashboard, drag-and-drop submission
- Cloud Kubernetes deployment (AKS/EKS) when ready to experiment with Azure credit

## Demo Strategy

- Primary: run locally, screen-share or record a 2–3 minute demo video linked from README
- Secondary: deploy to Render or Fly.io free tier for live access (note: cold start delay on free tier)
- README includes architecture diagram (Mermaid), setup instructions, and demo video link
