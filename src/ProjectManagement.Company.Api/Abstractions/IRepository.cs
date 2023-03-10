﻿using Ardalis.Specification;

namespace ProjectManagement.CompanyAPI.Abstractions;

public interface IRepository<T> : IRepositoryBase<T>
    where T : class, IAggregateRoot
{
}