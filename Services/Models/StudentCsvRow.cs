namespace DotNetCoreSqlDb.Services.Models
{
    public class StudentCsvRow
    {
        public Guid   Id          { get; set; }    // same as Student.ID
        public string Name        { get; set; }
        public string AccountId   { get; set; }    // maps to the new Account GUID
        public string Status      { get; set; }    = "Active";
        public string Email       { get; set; }    = "";  // always blank
        public decimal TotalCharge{ get; set; }    = 0;
    }
}