using RabbitMQ.Client;

namespace ADMIN.Data.Dto
{
    public class Filter
    {
        public string ? name {get;set;}
        public string ? email{get;set;}
        public DateTime ?From{get;set;}
        public DateTime ?To {get;set;}
        public bool pending {get;set;}=false;
        public int page{get;set;}=1;
        public int pageSize{get;set;}=10;
        public bool mine{get;set;}=false;
        public int mineId{get;set;}=0;
    }
}