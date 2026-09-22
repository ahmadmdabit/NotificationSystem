using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Domain;

namespace Shared.Application.Behaviors;

/// <summary>
/// Marker interface for write commands (transaction wrapping, logging categorization).
/// </summary>
public interface ICommand { }
