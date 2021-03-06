﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using BangazonAPI.Models;

namespace BangazonAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DepartmentsController : ControllerBase

    {
        private readonly IConfiguration _config;

        public DepartmentsController(IConfiguration config)
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
        public async Task<IActionResult> Get()
        {
            using (SqlConnection conn = Connection)
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT d.Id AS DepartmentId, 
                                      d.Name AS DepartmentName,
                                      d.Budget as DepartmentBudget
                                      FROM Department AS d";

                    SqlDataReader reader = await cmd.ExecuteReaderAsync();
                    List<Department> departments = new List<Department>();
                    
                    while (reader.Read())
                    {
                        Department department = new Department
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("DepartmentId")),
                            Name = reader.GetString(reader.GetOrdinal("DepartmentName")),
                            Budget = reader.GetInt32(reader.GetOrdinal("DepartmentBudget"))
                        };

                        departments.Add(department);
                    }
                    reader.Close();

                    return Ok(departments);
                }
            }
        }

        [HttpGet("{id}", Name = "GetDepartment")]
        public async Task<IActionResult> Get([FromRoute] int id, [FromQuery] string include)
        {
            Department department = null;

            if (include == null)
            {
                 department = GetDepartmentOnly(id);
            }
            else
            {
                 department = GetDepartmentWithEmployees(id);
            }
            if (department == null)
            {
                return NotFound($"No department found with ID of {id}");
            }
            else
            {
                return Ok(department);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Department department)
        {
            using (SqlConnection conn = Connection)
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"INSERT INTO Department (Name, Budget)
                                        OUTPUT INSERTED.Id
                                        VALUES (@name, @budget)";
                    cmd.Parameters.Add(new SqlParameter("@name", department.Name));
                    cmd.Parameters.Add(new SqlParameter("@budget", department.Budget));

                    int newId = (int)await cmd.ExecuteScalarAsync();
                    department.Id = newId;
                    return CreatedAtRoute("GetDepartment", new { id = newId }, department);
                }
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put([FromRoute] int id, [FromBody] Department department)
        {
            try
            {
                using (SqlConnection conn = Connection)
                {
                    conn.Open();
                    using (SqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"UPDATE Department
                                            SET Name = @name, 
                                            Budget = @budget
                                            WHERE Id = @id";
                        cmd.Parameters.Add(new SqlParameter("@name", department.Name));
                        cmd.Parameters.Add(new SqlParameter("@budget", department.Budget));
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
                if (!DepartmentExist(id))
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
                        cmd.CommandText = @"DELETE FROM Department WHERE Id = @id";
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
                if (!DepartmentExist(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        private bool DepartmentExist(int id)
        {
            using (SqlConnection conn = Connection)
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT Id
                        FROM Department
                        WHERE Id = @id";
                    cmd.Parameters.Add(new SqlParameter("@id", id));

                    SqlDataReader reader = cmd.ExecuteReader();
                    return reader.Read();
                }
            }
        }

        private Department GetDepartmentWithEmployees(int id)
        {
            using (SqlConnection conn = Connection)
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT d.Id AS DepartmentId, 
                        d.Name AS DepartmentName, 
                        d.Budget AS DepartmentBudget,
                        e.Id as EmployeeId,
                        e.FirstName AS EmployeeFirstName,
                        e.LastName AS EmployeeLastName,
                        e.isSupervisor AS EmployeeSupervisor,
                        e.ComputerId AS EmployeeComputerId,
                        e.Email AS EmployeeEmail,
                        e.LastName AS EmmployeeLastName,
                        e.DepartmentId AS EmployeeDepartmentId,
                        e.IsSupervisor AS EmployeeSupervisor,
                        e.Email AS EmployeeEmail
                        FROM Department d
                        LEFT JOIN Employee e ON d.Id = e.DepartmentId
                        WHERE d.Id = 1";
                    cmd.Parameters.Add(new SqlParameter("@id", id));

                    SqlDataReader reader = cmd.ExecuteReader();

                    List<Department> departments = new List<Department>();

                    Department department = null;

                    while (reader.Read())
                    {
                        var departmentId = reader.GetInt32(reader.GetOrdinal("DepartmentId"));
                        var departmentAlreadyAdded = departments.FirstOrDefault(d => d.Id == departmentId);

                        if (departmentAlreadyAdded == null)
                        {
                            department = new Department
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("DepartmentId")),
                                Budget = reader.GetInt32(reader.GetOrdinal("DepartmentBudget")),
                                Name = reader.GetString(reader.GetOrdinal("DepartmentName"))
                            };

                            var employee = new Employee()
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                                FirstName = reader.GetString(reader.GetOrdinal("EmployeeFirstName")),
                                LastName = reader.GetString(reader.GetOrdinal("EmployeeLastName")),
                                DepartmentId = reader.GetInt32(reader.GetOrdinal("EmployeeDepartmentId")),
                                ComputerId = reader.GetInt32(reader.GetOrdinal("EmployeeComputerId")),
                                IsSupervisor = reader.GetBoolean(reader.GetOrdinal("EmployeeSupervisor")),
                                Email = reader.GetString(reader.GetOrdinal("EmployeeEmail"))
                            };

                            department.Employees.Add(employee);
                            departments.Add(department);
                        }
                        else
                        {
                            var employee = new Employee()
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("EmployeeId")),
                                FirstName = reader.GetString(reader.GetOrdinal("EmployeeFirstName")),
                                LastName = reader.GetString(reader.GetOrdinal("EmployeeLastName")),
                                DepartmentId = reader.GetInt32(reader.GetOrdinal("EmployeeDepartmentId")),
                                ComputerId = reader.GetInt32(reader.GetOrdinal("EmployeeComputerId")),
                                IsSupervisor = reader.GetBoolean(reader.GetOrdinal("EmployeeSupervisor")),
                                Email = reader.GetString(reader.GetOrdinal("EmployeeEmail"))
                            };

                            departmentAlreadyAdded.Employees.Add(employee);
                        }
                    }
                    reader.Close();
                    return (department);
                }
            }
        }

        private Department GetDepartmentOnly(int id)
        {
            using (SqlConnection conn = Connection)
            {
                conn.Open();
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                    SELECT Id AS DepartmentId, 
                           Name AS DepartmentName, 
                           Budget AS DepartmentBudget
                    FROM Department";

                    cmd.Parameters.Add(new SqlParameter("@id", id));
                    SqlDataReader reader = cmd.ExecuteReader();

                    Department department = null;
                   
                        if (reader.Read())
                        {
                            department = new Department
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("DepartmentId")),
                                Budget = reader.GetInt32(reader.GetOrdinal("DepartmentBudget")),
                                Name = reader.GetString(reader.GetOrdinal("DepartmentName"))
                            };
                        }
                    return (department);
                }
            }
        }
    }
}
