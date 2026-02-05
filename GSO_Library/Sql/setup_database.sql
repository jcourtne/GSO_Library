-- Run as a PostgreSQL superuser (e.g. postgres)
CREATE ROLE gso_app WITH LOGIN PASSWORD 'gso_app_password';
CREATE DATABASE gso_library OWNER gso_app;
