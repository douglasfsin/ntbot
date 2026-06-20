using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NtBot.Api.Services;
using NtBot.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NtBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GridController : ControllerBase
{
    private readonly GridEngine _gridEngine;

    public GridController(GridEngine gridEngine)
    {
        _gridEngine = gridEngine;
    }

    [HttpPost]
    public async Task<IActionResult> CreateGridOrder([FromBody] CreateGridOrderRequest request)
    {
        try
        {
            // Get tenant ID from claims (assuming JWT contains tenant info)
            var tenantId = GetTenantIdFromClaims();

            var gridOrder = await _gridEngine.CreateGridOrderAsync(tenantId, request);

            return Ok(new
            {
                success = true,
                gridOrder = new
                {
                    id = gridOrder.Id,
                    symbol = gridOrder.Symbol,
                    basePrice = gridOrder.BasePrice,
                    stepSize = gridOrder.StepSize,
                    maxLevels = gridOrder.MaxLevels,
                    lotSize = gridOrder.LotSize,
                    useMartingale = gridOrder.UseMartingale,
                    isActive = gridOrder.IsActive,
                    createdAt = gridOrder.CreatedAt
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateGridOrder(Guid id, [FromBody] UpdateGridOrderRequest request)
    {
        try
        {
            await _gridEngine.UpdateGridOrderAsync(id, request);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteGridOrder(Guid id)
    {
        try
        {
            await _gridEngine.DeleteGridOrderAsync(id);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetActiveGridOrders()
    {
        try
        {
            var tenantId = GetTenantIdFromClaims();
            var gridOrders = await _gridEngine.GetActiveGridOrdersAsync(tenantId);

            var result = gridOrders.Select(go => new
            {
                id = go.Id,
                symbol = go.Symbol,
                basePrice = go.BasePrice,
                stepSize = go.StepSize,
                maxLevels = go.MaxLevels,
                lotSize = go.LotSize,
                useMartingale = go.UseMartingale,
                isActive = go.IsActive,
                currentLevel = go.Levels?.Count(l => l.IsFilled) ?? 0,
                totalProfit = go.Levels?.Where(l => l.IsFilled).Sum(l => CalculateLevelProfit(l)) ?? 0,
                createdAt = go.CreatedAt,
                levels = go.Levels?.Select(l => new
                {
                    level = l.Level,
                    price = l.Price,
                    volume = l.Volume,
                    direction = l.Direction.ToString(),
                    isFilled = l.IsFilled,
                    profit = l.IsFilled ? CalculateLevelProfit(l) : 0
                })
            });

            return Ok(new { success = true, gridOrders = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetGridOrder(Guid id)
    {
        try
        {
            var gridOrder = await _gridEngine.GetGridOrderAsync(id);

            if (gridOrder == null)
                return NotFound(new { success = false, message = "Grid order not found" });

            var result = new
            {
                id = gridOrder.Id,
                symbol = gridOrder.Symbol,
                basePrice = gridOrder.BasePrice,
                stepSize = gridOrder.StepSize,
                maxLevels = gridOrder.MaxLevels,
                lotSize = gridOrder.LotSize,
                useMartingale = gridOrder.UseMartingale,
                isActive = gridOrder.IsActive,
                currentLevel = gridOrder.Levels?.Count(l => l.IsFilled) ?? 0,
                totalProfit = gridOrder.Levels?.Where(l => l.IsFilled).Sum(l => CalculateLevelProfit(l)) ?? 0,
                createdAt = gridOrder.CreatedAt,
                levels = gridOrder.Levels?.Select(l => new
                {
                    level = l.Level,
                    price = l.Price,
                    volume = l.Volume,
                    direction = l.Direction.ToString(),
                    isFilled = l.IsFilled,
                    filledAt = l.FilledAt,
                    profit = l.IsFilled ? CalculateLevelProfit(l) : 0
                })
            };

            return Ok(new { success = true, gridOrder = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{id}/close")]
    public async Task<IActionResult> CloseGridOrder(Guid id)
    {
        try
        {
            await _gridEngine.DeleteGridOrderAsync(id); // This will close all positions
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    private Guid GetTenantIdFromClaims()
    {
        // Extract tenant ID from JWT claims
        // For now, return a default tenant ID
        return Guid.NewGuid(); // Placeholderid.NewGuid(); // Placeholder
    }

    private decimal CalculateLevelProfit(GridLevel level)
    {
        // Simplified profit calculation - in real implementation,
        // this would use current market price
        return 0; // Placeholder
    }
}