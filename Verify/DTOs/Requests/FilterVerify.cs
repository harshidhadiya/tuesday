using System.Security.Cryptography;
using MassTransit.SagaStateMachine;

namespace VERIFY.Data.Dto
{
    public class FilterVerify
    {
       public  int page{get;set;}=1;
       public  int pagesize{get;set;}=10;
        public string? name{get;set;}
        public bool verified{get;set;}=false;
        public bool pending{get;set;}=false;
        public bool mine{get;set;}=false;
        public int verifierId{get;set;}=0;
    }
}