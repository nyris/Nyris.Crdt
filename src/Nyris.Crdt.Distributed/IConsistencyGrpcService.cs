using System.Collections.Generic;
using System.ServiceModel;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.Distributed
{
    [ServiceContract]
    public interface IConsistencyGrpcService
    {
        /// <summary>
        /// Get's hashes of all crdts with a given type name
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        [OperationContract]
        IAsyncEnumerable<HashAndInstanceId> GetHashesAsync(string typeName);
    }
}