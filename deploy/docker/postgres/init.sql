-- Database per service: one PostgreSQL server locally, but each service owns its own database.
-- Schemas are created by each service's EF Core migrations.
CREATE DATABASE catalog;
CREATE DATABASE ordering;
