﻿using System;
using System.Security.Cryptography;
using System.Text;
using Nyris.Crdt.Interfaces;

namespace Nyris.Crdt.Distributed.Test.Unit.Helpers;

internal record MockUser(Guid Id, string Name) : IHashable
{
    public ReadOnlySpan<byte> CalculateHash()
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);

        md5.AppendData(Encoding.UTF8.GetBytes(Name));

        return md5.GetCurrentHash();
    }
}
