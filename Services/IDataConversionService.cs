using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNetCoreSqlDb.Services
{
    /// <summary>
    /// Defines a service that takes a list of Student/Contact,
    /// generates two CSV byte arrays, and updates the tracking JSON.
    /// </summary>
    public interface IDataConversionService
    {
        /// <summary>
        /// Returns two CSV byte arrays: (accountCsv, studentCsv)
        /// and updates the tracking JSON on disk.
        /// </summary>
        /// <param name="students">All students from EF</param>
        /// <param name="contacts">All contacts from EF</param>
        Task<(byte[] accountCsv, byte[] studentCsv)> GenerateCsvFilesAsync(
            IEnumerable<DotNetCoreSqlDb.Models.Student> students,
            IEnumerable<DotNetCoreSqlDb.Models.Contact> contacts);
    }
}
