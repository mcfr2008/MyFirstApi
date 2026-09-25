-- Feature: Products
-- Schema for MyFirstApi.Models.Product / ProductsController.

CREATE TABLE IF NOT EXISTS "Products" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(255) NOT NULL,
    "Price" NUMERIC(18, 2) NOT NULL
);
