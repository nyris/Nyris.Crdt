using System.Collections.Generic;
using System.ServiceModel;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed.Services
{
    [ServiceContract]
    public interface IConsistencyGrpcService
    {
        [OperationContract]
        IAsyncEnumerable<WithId<TypeNameAndHash>> GetHashesAsync();
    }
}