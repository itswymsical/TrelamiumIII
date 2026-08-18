using TrelamiumIII.Common.Systems.Jobs;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Common.Systems.Jobs;

public class Task(string descriptionKey, int requiredCount = 1)
{
    public string DescriptionKey { get; set; } = descriptionKey;

    // Resolve description at runtime
    public string Description => TrelamiumIII.Instance.GetLocalization($"Jobs.{DescriptionKey}").Value;

    public bool IsCompleted { get; set; } = false;
    public int RequiredCount { get; set; } = requiredCount;
    public int Count { get; set; } = 0;
    public bool IsVisible { get; set; } = true;

    public delegate bool VisibilityCondition(Job Job);
    public VisibilityCondition VisibilityCheck { get; set; } = null;

    public bool ShouldBeVisible(Job Job)
    {
        if (VisibilityCheck != null)
            return VisibilityCheck(Job);

        return IsVisible;
    }

    public bool UpdateProgress(int amount = 1)
    {
        Count += amount;
        if (Count >= RequiredCount)
        {
            IsCompleted = true;
            Count = RequiredCount;
            return true;
        }
        return false;
    }

    public void WriteData(BinaryWriter writer)
    {
        writer.Write(DescriptionKey); // Save the key, not resolved text
        writer.Write(IsCompleted);
        writer.Write(RequiredCount);
        writer.Write(Count);
        writer.Write(IsVisible);
    }

    public void ReadData(BinaryReader reader)
    {
        DescriptionKey = reader.ReadString();
        IsCompleted = reader.ReadBoolean();
        RequiredCount = reader.ReadInt32();
        Count = reader.ReadInt32();
        IsVisible = reader.ReadBoolean();
    }

    public byte[] Serialize()
    {
        using MemoryStream ms = new();
        using (BinaryWriter writer = new(ms))
        {
            WriteData(writer);
        }
        return ms.ToArray();
    }

    public void Deserialize(byte[] data)
    {
        using MemoryStream ms = new(data);
        using BinaryReader reader = new(ms);
        ReadData(reader);
    }
}

public class TaskIndexState
{
    public List<TaskState> Tasks { get; set; } = [];

    public TagCompound Serialize()
    {
        return new TagCompound
        {
            ["Task"] = Tasks.Select(obj => obj.Serialize()).ToList()
        };
    }

    public static TaskIndexState Deserialize(TagCompound tag)
    {
        try
        {
            return new TaskIndexState
            {
                Tasks = tag.GetList<TagCompound>("Task")
                    .Select(t => TaskState.Deserialize(t))
                    .Where(obj => obj != null)
                    .ToList()
            };
        }
        catch
        {
            return new TaskIndexState();
        }
    }
}

public class TaskState
{
    public string Description { get; set; }
    public bool IsCompleted { get; set; }
    public int RequiredCount { get; set; }
    public int CurrentCount { get; set; }

    public TagCompound Serialize()
    {
        return new TagCompound
        {
            ["Description"] = Description,
            ["IsCompleted"] = IsCompleted,
            ["RequiredCount"] = RequiredCount,
            ["Count"] = CurrentCount
        };
    }

    public static TaskState Deserialize(TagCompound tag)
    {
        try
        {
            return new TaskState
            {
                Description = tag.GetString("Description"),
                IsCompleted = tag.GetBool("IsCompleted"),
                RequiredCount = tag.GetInt("RequiredCount"),
                CurrentCount = tag.GetInt("Count")
            };
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// gets a list of tasks for a Job. Jobs typically have serveral task lists that need to be completed.
/// </summary>
/// <param name="tasks"></param>
public class TaskList(List<Task> tasks)
{
    /// <summary>
    /// gets the current task index.
    /// </summary>
    public List<Task> Task { get; } = tasks;
    public bool IsCompleted => Task.All(o => o.IsCompleted);
    public bool HasCheckedInitialInventory { get; set; } = false;

    public void Reset()
    {
        foreach (var task in Task)
        {
            task.IsCompleted = false;
            task.Count = 0;
        }
        HasCheckedInitialInventory = false;
    }

    public void WriteData(BinaryWriter writer)
    {
        writer.Write(Task.Count);
        writer.Write(HasCheckedInitialInventory);
        foreach (var task in Task)
        {
            task.WriteData(writer);
        }
    }

    public void ReadData(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        HasCheckedInitialInventory = reader.ReadBoolean();
        Task.Clear();
        for (var i = 0; i < count; i++)
        {
            var task = new Task("", 1);
            task.ReadData(reader);
            Task.Add(task);
        }
    }

    public byte[] Serialize()
    {
        using MemoryStream ms = new();
        using (BinaryWriter writer = new(ms))
        {
            WriteData(writer);
        }
        return ms.ToArray();
    }

    public void Deserialize(byte[] data)
    {
        using MemoryStream ms = new(data);
        using BinaryReader reader = new(ms);
        ReadData(reader);
    }
}