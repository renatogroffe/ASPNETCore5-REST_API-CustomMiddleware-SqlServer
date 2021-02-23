using System;
using System.Threading.Tasks;
using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;

namespace ExemploAPIMiddleware
{
    public class ChecagemIndisponibilidade
    {
        private readonly RequestDelegate _next;

        public ChecagemIndisponibilidade(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var config = (IConfiguration)httpContext
                .RequestServices.GetService(typeof(IConfiguration));
            var logger = (ILogger<ChecagemIndisponibilidade>)httpContext
                .RequestServices.GetService(typeof(ILogger<ChecagemIndisponibilidade>));
            string mensagem = null;

            using var conexao = new SqlConnection(
                config.GetConnectionString("BaseConfigDisponibilidade"));
            conexao.Open();

            var cmd = conexao.CreateCommand();
            cmd.CommandText =
                "SELECT TOP 1 Mensagem FROM Indisponibilidade " +
                "WHERE @DataProcessamento BETWEEN InicioIndisponibilidade " +
                    "AND TerminoIndisponibilidade " +
                "ORDER BY InicioIndisponibilidade";
            cmd.Parameters.Add("@DataProcessamento",
                SqlDbType.DateTime).Value = DateTime.Now;

            logger.LogInformation(
                "Analisando se a aplicacao deve ser considerada como indisponivel...");
            var reader = cmd.ExecuteReader();
            if (reader.Read())
                mensagem = reader["Mensagem"].ToString();

            conexao.Close();

            if (mensagem == null)
            {
                logger.LogInformation("Acesso liberado a aplicacao...");
                await _next(httpContext);
            }
            else
            {
                logger.LogError(
                    $"Aplicacao configurada como indisponivel - Mensagem de retorno: {mensagem}");
                httpContext.Response.StatusCode = 403;
                httpContext.Response.ContentType = "application/json";
                
                var status = new
                {
                    Codigo = 403,
                    Status = "Forbidden",
                    Mensagem = mensagem
                };
                
                await httpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(status));
            }
        }
    }

    public static class ChecagemIndisponibilidadeExtensions
    {
        public static IApplicationBuilder UseChecagemIndisponibilidade(
            this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ChecagemIndisponibilidade>();
        }
    }
}