using System;
using System.Collections.Generic;
using System.Linq;
using Orbital.Core.Models;
using Marketdata.Database.Models;

namespace Orbital.Core.Services;

public class ZoneInterestManager
{
    private readonly List<PriceZone> _activeZones = new();
    private readonly object _lock = new();

    // Sincroniza em memória as zonas calculadas pelo QuestDB ou marcadas manualmente
    public void UpdateZonesFromRecords(IEnumerable<PointOfInterestRecord> poiRecords)
    {
        var mappedZones = poiRecords
            .Where(r => r.IsActive)
            .Select(r => new PriceZone
            {
                Id = r.PoiId,
                Type = r.Type,
                Side = r.Side,
                Description = r.Description,
                // O QuestDB armazena PriceEnd como NaN se for null.
                // Aqui tratamos se é um ponto exato ou uma zona.
                HighPrice = r.PriceEnd.HasValue && !double.IsNaN(r.PriceEnd.Value) 
                    ? Math.Max(r.PriceStart, r.PriceEnd.Value) 
                    : r.PriceStart,
                LowPrice = r.PriceEnd.HasValue && !double.IsNaN(r.PriceEnd.Value) 
                    ? Math.Min(r.PriceStart, r.PriceEnd.Value) 
                    : r.PriceStart,
                TrackedVolume = 0,
                IsActive = true
            });

        lock (_lock)
        {
            _activeZones.Clear();
            _activeZones.AddRange(mappedZones);
        }
    }

    public void UpdateZonesFromQuery(IEnumerable<PriceZone> freshZones)
    {
        lock (_lock)
        {
            _activeZones.Clear();
            _activeZones.AddRange(freshZones);
        }
    }

    public void AddSingleZoneFromRecord(PointOfInterestRecord poiRecord)
    {
        if (!poiRecord.IsActive) return;

        var zone = new PriceZone
        {
            Id = poiRecord.PoiId,
            Type = poiRecord.Type,
            Side = poiRecord.Side,
            Description = poiRecord.Description,
            HighPrice = poiRecord.PriceEnd.HasValue && !double.IsNaN(poiRecord.PriceEnd.Value) 
                ? Math.Max(poiRecord.PriceStart, poiRecord.PriceEnd.Value) 
                : poiRecord.PriceStart,
            LowPrice = poiRecord.PriceEnd.HasValue && !double.IsNaN(poiRecord.PriceEnd.Value) 
                ? Math.Min(poiRecord.PriceStart, poiRecord.PriceEnd.Value) 
                : poiRecord.PriceStart,
            TrackedVolume = 0,
            IsActive = true
        };

        lock (_lock)
        {
            // Remove previous zone with same ID if exists
            _activeZones.RemoveAll(z => z.Id == zone.Id);
            _activeZones.Add(zone);
        }
    }

    // Chamado a cada tick para validar se o preço está testando uma POI
    public PriceZone? CheckCollision(double lastMarketPrice)
    {
        lock (_lock)
        {
            return _activeZones.FirstOrDefault(z => z.IsActive && 
                                                    lastMarketPrice >= z.LowPrice && 
                                                    lastMarketPrice <= z.HighPrice);
        }
    }
}
