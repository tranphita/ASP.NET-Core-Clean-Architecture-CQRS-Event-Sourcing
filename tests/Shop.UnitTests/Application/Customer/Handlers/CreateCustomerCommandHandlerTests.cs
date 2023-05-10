using System;
using System.Threading;
using System.Threading.Tasks;
using Bogus;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Shop.Application.Customer.Commands;
using Shop.Application.Customer.Handlers;
using Shop.Core.Abstractions;
using Shop.Core.Events;
using Shop.Domain.Entities.CustomerAggregate;
using Shop.Domain.Factories;
using Shop.Infrastructure.Data;
using Shop.Infrastructure.Data.Repositories;
using Shop.UnitTests.Fixtures;
using Xunit;
using Xunit.Categories;

namespace Shop.UnitTests.Application.Customer.Handlers;

[UnitTest]
public class CreateCustomerCommandHandlerTests : IClassFixture<EfSqliteFixture>
{
    private readonly EfSqliteFixture _fixture;
    private readonly CreateCustomerCommandValidator _validator = new();

    public CreateCustomerCommandHandlerTests(EfSqliteFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Add_ValidCommand_ShouldReturnsSuccessResult()
    {
        // Arrange
        var command = new Faker<CreateCustomerCommand>()
            .RuleFor(command => command.FirstName, faker => faker.Person.FirstName)
            .RuleFor(command => command.LastName, faker => faker.Person.LastName)
            .RuleFor(command => command.Gender, faker => faker.PickRandom<EGender>())
            .RuleFor(command => command.Email, faker => faker.Person.Email.ToLowerInvariant())
            .RuleFor(command => command.DateOfBirth, faker => faker.Person.DateOfBirth)
            .Generate();

        var unitOfWork = new UnitOfWork(
            _fixture.Context,
            Mock.Of<IEventStoreRepository>(),
            Mock.Of<IMediator>(),
            Mock.Of<ILogger<UnitOfWork>>());

        var handler = new CreateCustomerCommandHandler(
            _validator,
            new CustomerWriteOnlyRepository(_fixture.Context),
            unitOfWork);

        // Act
        var act = await handler.Handle(command, CancellationToken.None);

        // Assert
        act.Should().NotBeNull();
        act.IsSuccess.Should().BeTrue();
        act.SuccessMessage.Should().Be("Cadastrado com sucesso!");
        act.Value.Should().NotBeNull();
        act.Value.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Add_DuplicateEmailCommand_ShouldReturnsFailResult()
    {
        // Arrange
        var command = new Faker<CreateCustomerCommand>()
            .RuleFor(command => command.FirstName, faker => faker.Person.FirstName)
            .RuleFor(command => command.LastName, faker => faker.Person.LastName)
            .RuleFor(command => command.Gender, faker => faker.PickRandom<EGender>())
            .RuleFor(command => command.Email, faker => faker.Person.Email.ToLowerInvariant())
            .RuleFor(command => command.DateOfBirth, faker => faker.Person.DateOfBirth)
            .Generate();

        var repository = new CustomerWriteOnlyRepository(_fixture.Context);
        repository.Add(CustomerFactory.Create(
            command.FirstName,
            command.LastName,
            command.Gender,
            command.Email,
            command.DateOfBirth));

        await _fixture.Context.SaveChangesAsync();
        _fixture.Context.ChangeTracker.Clear();

        var handler = new CreateCustomerCommandHandler(
            _validator,
            repository,
            Mock.Of<IUnitOfWork>());

        // Act
        var act = await handler.Handle(command, CancellationToken.None);

        // Assert
        act.Should().NotBeNull();
        act.IsSuccess.Should().BeFalse();
        act.Errors.Should()
            .NotBeNullOrEmpty()
            .And.OnlyHaveUniqueItems()
            .And.Contain(errorMessage => errorMessage == "O endereço de e-mail informado já está sendo utilizado.");
    }

    [Fact]
    public async Task Add_InvalidCommand_ShouldReturnsFailResult()
    {
        // Arrange
        var handler = new CreateCustomerCommandHandler(
            _validator,
            Mock.Of<ICustomerWriteOnlyRepository>(),
            Mock.Of<IUnitOfWork>());

        // Act
        var act = await handler.Handle(new CreateCustomerCommand(), CancellationToken.None);

        // Assert
        act.Should().NotBeNull();
        act.IsSuccess.Should().BeFalse();
        act.ValidationErrors.Should().NotBeNullOrEmpty().And.OnlyHaveUniqueItems();
    }
}