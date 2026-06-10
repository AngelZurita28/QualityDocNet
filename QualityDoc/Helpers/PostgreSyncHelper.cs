using System;
using System.Data;
using System.Threading.Tasks;
using Npgsql;
using QualityDoc.Pages.Models;

namespace QualityDoc.Helpers
{
    public static class PostgreSyncHelper
    {
        public static async Task<bool> SyncToPostgreAsync(Documento documento, string connectionString)
        {
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            // 1. Si este documento va a ser marcado como el más reciente,
            // quitamos la bandera 'is_latest' a las versiones anteriores del mismo código.
            if (documento.IsLatest)
            {
                using var updateCmd = new NpgsqlCommand(
                    "UPDATE documents SET is_latest = FALSE WHERE document_code = @code", conn);
                updateCmd.Parameters.AddWithValue("code", documento.DocumentCode ?? (object)DBNull.Value);
                await updateCmd.ExecuteNonQueryAsync();
            }

            // 2. Insertamos o actualizamos (UPSERT) el documento
            var sql = @"
                INSERT INTO documents (id, document_code, version_number, is_latest, title, description, file_path, company_name, status_name)
                VALUES (@id, @code, @version, @latest, @title, @description, @file, @company, @status)
                ON CONFLICT (id) DO UPDATE SET
                    document_code = EXCLUDED.document_code,
                    version_number = EXCLUDED.version_number,
                    is_latest = EXCLUDED.is_latest,
                    title = EXCLUDED.title,
                    description = EXCLUDED.description,
                    file_path = EXCLUDED.file_path,
                    company_name = EXCLUDED.company_name,
                    status_name = EXCLUDED.status_name;";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", documento.Id);
            cmd.Parameters.AddWithValue("code", documento.DocumentCode ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("version", documento.VersionNumber.HasValue ? (object)documento.VersionNumber.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("latest", documento.IsLatest);
            cmd.Parameters.AddWithValue("title", documento.Title);
            cmd.Parameters.AddWithValue("description", documento.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("file", documento.FilePath ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("company", documento.Company?.Name ?? "N/A");
            cmd.Parameters.AddWithValue("status", documento.Status?.Name ?? "Activo");

            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
    }
}
