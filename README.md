# Markain Management System

The Markain Management System is an integrated institutional platform designed for comprehensive organizational management. It centralizes human resources, training, recruitment, and professional collaboration into a single operational hub.

## System Overview

The application provides a suite of modules tailored for institutional efficiency:

- Dashboard: A real-time command center providing an operational overview of active modules and system status.
- Training Hub: A centralized library for managing and delivering institutional training modules.
- Recruitment (ATS): An Applicant Tracking System for managing job postings and candidate cycles.
- Careers Portal: A public-facing interface for institutional talent acquisition.
- Collaboration HUB: A secure communication environment for private and group collaboration.
- Administrative Suite: High-level system configuration and institutional intelligence tools.
- PDF Toolbox: Professional document processing utilities.

## Technology Stack

The system is built on a modern enterprise architecture:

- Framework: ASP.NET Core MVC
- Persistence: Entity Framework Core with PostgreSQL
- Styling: Tailwind CSS
- Iconography: Lucide

## Execution Guide

### Prerequisites

Ensure the following components are installed:

- .NET 8.0 SDK or later
- PostgreSQL Database Server

### Running the Application

1. Configure the database connection string in appsettings.json or via environment variables.
2. Apply the latest migrations:
   dotnet ef database update
3. Build and launch the system:
   dotnet run --project MarkainHRM

The application will be accessible at the institutional endpoint (default: https://localhost:7107 or http://localhost:5107).
