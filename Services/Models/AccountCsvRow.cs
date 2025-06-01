namespace DotNetCoreSqlDb.Services.Models
{
    public class AccountCsvRow
    {
        public string Id          { get; set; }    // new GUID as string
        public string Name        { get; set; }    // ParentOrEmployer or student Name
        public decimal Balance    { get; set; }    = 0;
        public string Status      { get; set; }    = "";
        public string Email       { get; set; }    = "";
        public decimal TotalCharge{ get; set; }    = 0;
        public decimal TotalPay   { get; set; }    = 0;
    }
}