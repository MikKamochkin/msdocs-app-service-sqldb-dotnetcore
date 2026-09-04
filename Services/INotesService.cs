using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNetCoreSqlDb.Services
{
    public interface INotesService
    {
        Task CleanupNotesAsync();
    }
}