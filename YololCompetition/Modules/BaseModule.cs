using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;

namespace YololCompetition.Modules
{
    public class BaseModule
        : ModuleBase
    {
        protected async Task DisplayItemList<T>(IEnumerable<T> items, Func<string> nothing, Func<T, int, string> itemToString)
        {
            await DisplayItemList(items, nothing, (a, b) => Task.FromResult(itemToString(a, b)));
        }

        protected async Task DisplayItemList<T>(IEnumerable<T> items, Func<string> nothing, Func<T, int, Task<string>> itemToString)
        {
            var builder = new StringBuilder();

            var none = true;
            var index = 0;
            foreach (var item in items)
            {
                none = false;    

                var str = await itemToString(item, index++);
                if (builder.Length + str.Length > 1000)
                {
                    await ReplyAsync(builder.ToString());
                    builder.Clear();
                }

                builder.Append(str);
                builder.Append('\n');
            }

            if (builder.Length > 0)
                await ReplyAsync(builder.ToString());

            if (none)
                await ReplyAsync(nothing());
        }
    }
}
