using MassTransit;
using Messaging.Contracts;
using USER.Model;

namespace USER.Messaging.Consumer
{
    public class refreshTokenConsumer : IConsumer<RefreshTokenGenerate>
    {
        private readonly MACUTIONDB db;
        private readonly ILogger<refreshTokenConsumer> logger;
        public refreshTokenConsumer(MACUTIONDB db,ILogger<refreshTokenConsumer> logger)
        {
            this.db=db;
            this.logger=logger;
        }
        public async  Task Consume(ConsumeContext<RefreshTokenGenerate> context)
        {

            var datas=context.Message;
            if(string.IsNullOrEmpty(datas.name) || string.IsNullOrEmpty(datas.role) || string.IsNullOrEmpty(datas.refreshToken) || datas.userId<=0 ){
            logger.LogError("sorry but some data has to be required to be run right ");
            return ;
            }
       
            var addingData=new RefreshTable{expiryDate=datas.expiryDate,name=datas.name,refreshToken=datas.refreshToken,role=datas.role,userId=datas.userId};
            var database=db.refreshTables.Add(addingData);

            await db.SaveChangesAsync();
            logger.LogInformation("RefreshToken Saved Successfully");
        }
    }
}