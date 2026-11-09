using System;
using System.Collections.Generic;
using System.Linq;
namespace SV_SOUL.Models;
public sealed record PassiveFestivalContext(string Name,string Season,int StartDay,int EndDay,int StartTime,bool Permitted);
public static class FestivalContext
{
    public static IReadOnlyList<PassiveFestivalContext> Active(IEnumerable<PassiveFestivalContext> entries,string season,int day)=>entries.Where(e=>e.Permitted && e.Season.Equals(season,StringComparison.OrdinalIgnoreCase) && day>=e.StartDay && day<=e.EndDay).ToArray();
}
