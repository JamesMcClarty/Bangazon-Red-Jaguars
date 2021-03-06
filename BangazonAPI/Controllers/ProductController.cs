﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;
using BangazonAPI.Models;
using Microsoft.AspNetCore.Http;

namespace BangazonAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly IConfiguration _config;

        public ProductsController(IConfiguration config)
        {
            _config = config;
        }

        public SqlConnection Connection
        {
            get
            {
                return new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery]string q, [FromQuery] bool? asc, [FromQuery] int? ItemsPerPage, [FromQuery] int? currentPage, [FromQuery]string sortBy = "recent")
        {
            using (SqlConnection conn = Connection)
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT  p.Id, p.ProductTypeId, p.CustomerId, p.Price, p.[Description], p.Title, p.DateAdded, COUNT(op.ProductId) AS PopularityIndex, overall_count = COUNT(*) OVER() FROM Product p                                      
                                       LEFT JOIN OrderProduct op ON p.Id = op.ProductId
                                       GROUP BY p.Id, p.ProductTypeId, p.CustomerId, p.Price, p.[Description], p.Title, p.DateAdded                                       
                                       HAVING 1=1";

                    if (!string.IsNullOrWhiteSpace(q))
                    {
                        cmd.CommandText += " AND (p.Title LIKE @Search) OR (p.[Description] LIKE @Search)";
                        cmd.Parameters.Add(new SqlParameter(@"Search", "%" + q + "%"));
                    }

                    if (sortBy == "recent")
                    {
                        cmd.CommandText += " ORDER BY p.DateAdded DESC";                       
                    }

                    if (sortBy == "popularity")
                    {
                        cmd.CommandText += " ORDER BY COUNT(op.ProductId) DESC";
                    }

                    if (sortBy == "price" && asc == true)
                    {
                        cmd.CommandText += " ORDER BY p.Price";
                    }

                    if (sortBy == "price" && asc == false)
                    {
                        cmd.CommandText += " ORDER BY p.Price DESC";
                    }

                    if (ItemsPerPage != null && currentPage != null)
                    {
                            cmd.CommandText += " OFFSET @offset ROWS FETCH NEXT @items ROWS ONLY ";
                            cmd.Parameters.Add(new SqlParameter(@"offset", (currentPage - 1) * ItemsPerPage));
                            cmd.Parameters.Add(new SqlParameter(@"items", ItemsPerPage));                        
                    }

                    SqlDataReader reader = await cmd.ExecuteReaderAsync();

                    List<Product> products = new List<Product>();

                    var totalRows = 0;

                    while (reader.Read())
                    {
                        totalRows = reader.GetInt32(reader.GetOrdinal("overall_count"));
                        Product product = new Product
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            ProductTypeId = reader.GetInt32(reader.GetOrdinal("ProductTypeId")),
                            CustomerId = reader.GetInt32(reader.GetOrdinal("CustomerId")),
                            Price = reader.GetDecimal(reader.GetOrdinal("Price")),
                            Description = reader.GetString(reader.GetOrdinal("Description")),
                            Title = reader.GetString(reader.GetOrdinal("Title")),
                            DateAdded = reader.GetDateTime(reader.GetOrdinal("DateAdded")),                        
                        };
                        products.Add(product);
                    }
                    reader.Close();

                    Response.Headers.Add("X-Total-Count", totalRows.ToString());

                    return Ok(products);
                }
            }
        }


        [HttpGet("{id}", Name = "GetProduct")]
        public async Task<IActionResult> Get([FromRoute] int id)
        {
            using (SqlConnection conn = Connection)
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT p.Id, p.ProductTypeId, p.CustomerId, p.Price, p.[Description], p.Title, p.DateAdded FROM Product p 
                                        WHERE p.Id = @id";
                    cmd.Parameters.Add(new SqlParameter("@id", id));

                    SqlDataReader reader = await cmd.ExecuteReaderAsync();

                    Product product = null;

                    if (reader.Read())
                    {
                        product = new Product
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            ProductTypeId = reader.GetInt32(reader.GetOrdinal("ProductTypeId")),
                            CustomerId = reader.GetInt32(reader.GetOrdinal("CustomerId")),
                            Price = reader.GetDecimal(reader.GetOrdinal("Price")),
                            Description = reader.GetString(reader.GetOrdinal("Description")),
                            Title = reader.GetString(reader.GetOrdinal("Title")),
                            DateAdded = reader.GetDateTime(reader.GetOrdinal("DateAdded")),
                         };
                    }
                    reader.Close();

                    if (product == null)
                    {
                        return NotFound($"No Product found with the ID of {id}");
                    }
                    return Ok(product);
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Product product)
        {
            using (SqlConnection conn = Connection)
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"INSERT INTO Product (DateAdded, ProductTypeId, CustomerId, Price, Title, Description)
                                        OUTPUT INSERTED.Id
                                        VALUES (@DateAdded, @ProductTypeId, @CustomerId, @Price, @Title, @Description)";
                    cmd.Parameters.Add(new SqlParameter("@DateAdded", DateTime.Now));
                    cmd.Parameters.Add(new SqlParameter("@ProductTypeId", product.ProductTypeId));
                    cmd.Parameters.Add(new SqlParameter("@CustomerId", product.CustomerId));
                    cmd.Parameters.Add(new SqlParameter("@Price", product.Price));
                    cmd.Parameters.Add(new SqlParameter("@Title", product.Title));
                    cmd.Parameters.Add(new SqlParameter("@Description", product.Description));

                    int newId = (int)await cmd.ExecuteScalarAsync();

                    product.Id = newId;

                    return CreatedAtRoute("GetProduct", new { id = newId }, product);
                }
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put([FromRoute] int id, [FromBody] Product product)
        {
            try
            {
                using (SqlConnection conn = Connection)
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"UPDATE Product
                                                    SET DateAdded = @DateAdded,
                                                        ProductTypeId = @ProductTypeId,
                                                        CustomerId = @CustomerId,
                                                        Price = @Price,
                                                        Title = @Title,
                                                        Description = @Description
                                                    WHERE Id = @id";
                        cmd.Parameters.Add(new SqlParameter("@DateAdded", product.DateAdded));
                        cmd.Parameters.Add(new SqlParameter("@ProductTypeId", product.ProductTypeId));
                        cmd.Parameters.Add(new SqlParameter("@CustomerId", product.CustomerId));
                        cmd.Parameters.Add(new SqlParameter("@Price", product.Price));
                        cmd.Parameters.Add(new SqlParameter("@Title", product.Title));
                        cmd.Parameters.Add(new SqlParameter("@Description", product.Description));
                        cmd.Parameters.Add(new SqlParameter("@id", id));

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return new StatusCodeResult(StatusCodes.Status204NoContent);
                        }
                        return BadRequest($"No Product with Id of {id}");
                    }
                }
            }
            catch (Exception)
            {
                if (!ProductExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            try
            {
                using (SqlConnection conn = Connection)
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"DELETE FROM Product WHERE Id = @id";
                        cmd.Parameters.Add(new SqlParameter("@id", id));

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return new StatusCodeResult(StatusCodes.Status204NoContent);
                        }
                        throw new Exception("No rows affected");
                    }
                }
            }
            catch (Exception)
            {
                if (!ProductExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        private bool ProductExists(int id)
        {
            using (SqlConnection conn = Connection)
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                                SELECT DateAdded, ProductTypeId, CustomerId, Price, Title, Description
                                FROM Product
                                WHERE Id = @id";
                    cmd.Parameters.Add(new SqlParameter("@id", id));

                    SqlDataReader reader = cmd.ExecuteReader();

                    return reader.Read();
                }
            }
        }
    }
}
