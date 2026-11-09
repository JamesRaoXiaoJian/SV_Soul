using System.Collections.Generic;
namespace SV_SOUL.Storage;
/// <summary>A repository instance belongs to one save/player; NPC is scoped within it.</summary>
public interface IMemoryRepository
{
    List<ConversationTurn> LoadHistory(string npc);
    List<ConversationTurn> LoadHistoryPage(string npc, int limit);
    void SaveHistory(string npc,List<ConversationTurn> turns);
    NPCMemory LoadMemories(string npc);
    string GetMemoriesSummary(string npc,string query="");
    void MergeStatements(string npc,IEnumerable<MemoryRecord> records);
    void MarkExtracted(string npc,int count);
}
