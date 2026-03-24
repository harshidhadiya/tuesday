using ADMIN.Repositories;
using MassTransit;
using Messaging.Contracts;

namespace ADMIN.Messaging.Consumers;

public class adminUpdateConsumer (IRequestRepository _repo) : IConsumer<AdminUpdate>
{
    
    public async Task Consume(ConsumeContext<AdminUpdate> context)
    {
        var user=await _repo.GetRequestByUserIdAsync(context.Message.AdminId);
        if(user==null)
        return ;
        if(!string.IsNullOrWhiteSpace(context.Message.Name))
        user.Name=context.Message.Name;

        if (!string.IsNullOrWhiteSpace(context.Message.Email))
        {
            user.Email=context.Message.Email;
        }

        await _repo.UpdateRequestAsync(user);
        
    }
}