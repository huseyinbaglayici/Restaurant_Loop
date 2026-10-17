using System.Collections.Generic;
using RestaurantLoop.Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace RestaurantLoop.Editor.LevelEditor
{
    // Undoable whole-queue operations used by the Conveyor Grid window's queue section.
    internal static class QueueLaneEdits
    {
        private const int MinChunk = 3;
        private const int MaxChunk = 5;

        // food total per type -> chunks of 3-5 -> dealt round-robin across lanes
        public static void GenerateFromCustomers(ConveyorLevelDataSO data)
        {
            Undo.RecordObject(data, "Generate Queue From Customers");

            var totals = new Dictionary<FoodType, int>();
            foreach (var customer in data.Customers)
            {
                totals[customer.Order.Type] = totals.GetValueOrDefault(customer.Order.Type) + customer.Order.Amount;
                if (customer.HasSecondOrder)
                    totals[customer.SecondOrder.Type] = totals.GetValueOrDefault(customer.SecondOrder.Type) + customer.SecondOrder.Amount;
            }

            foreach (var lane in data.Queues) lane.Entries.Clear();

            int laneIndex = 0;
            foreach (var kv in totals)
            {
                int remaining = kv.Value;
                while (remaining > 0)
                {
                    int chunkSize = Random.Range(MinChunk, MaxChunk + 1);
                    if (remaining <= MaxChunk) chunkSize = remaining;
                    else if (remaining - chunkSize < MinChunk) chunkSize = remaining - MinChunk;
                    if (chunkSize < MinChunk) chunkSize = remaining;

                    remaining -= chunkSize;
                    data.Queues[laneIndex % data.Queues.Count].Entries.Add(new FoodStackData { Type = kv.Key, Amount = chunkSize });
                    laneIndex++;
                }
            }
        }

        // keeps each entry's LinkId; only the order and lane change
        public static void Shuffle(ConveyorLevelDataSO data)
        {
            Undo.RecordObject(data, "Shuffle Queue");

            var all = new List<FoodStackData>();
            foreach (var lane in data.Queues)
            {
                all.AddRange(lane.Entries);
                lane.Entries.Clear();
            }

            for (int i = 0; i < all.Count; i++)
            {
                int j = Random.Range(i, all.Count);
                (all[i], all[j]) = (all[j], all[i]);
            }

            for (int i = 0; i < all.Count; i++)
                data.Queues[i % data.Queues.Count].Entries.Add(all[i]);
        }
    }
}
