using System;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;
using DocumentFormat.OpenXml.Vml;

namespace WbStarBot.Telegram.Extensions
{
    public static class TelegramExtensions
    {
        public static InlineKeyboardMarkup Markup(this (string, string)[] buttons)
        {
            InlineKeyboardButton[][] inlineKeyboardButtons = new InlineKeyboardButton[buttons.Length][];

            for (int i = 0; i < buttons.Length; i++)
            {
                (string, string) line = buttons[i];

                if (line.Item2.StartsWith("tg:") || line.Item2.StartsWith("http"))
                {
                    inlineKeyboardButtons[i] = new InlineKeyboardButton[]{ new InlineKeyboardButton(line.Item1)
                {
                    Url = line.Item2,
                }};
                }
                else
                {
                    inlineKeyboardButtons[i] = new InlineKeyboardButton[]{ new InlineKeyboardButton(line.Item1)
                {
                    CallbackData = line.Item2,
                }};
                }

            }

            return inlineKeyboardButtons;
        }

        public static InlineKeyboardMarkup Markup(this (string, string) button)
        {
            InlineKeyboardButton inlineKeyboardButtons = new InlineKeyboardButton(button.Item1);
            if (button.Item2.StartsWith("tg:") || button.Item2.StartsWith("http"))
            {
                inlineKeyboardButtons.Url = button.Item2;
            }
            else
            {
                inlineKeyboardButtons.CallbackData = button.Item2;
            }

            return inlineKeyboardButtons;
        }


        public static InlineKeyboardMarkup Markup(this (string, string)[][] buttons)
        {
            InlineKeyboardButton[][] inlineKeyboardButtons = new InlineKeyboardButton[buttons.Length][];

            for (int i = 0; i < buttons.Length; i++)
            {
                (string, string)[] line = buttons[i];
                inlineKeyboardButtons[i] = new InlineKeyboardButton[line.Length];

                for (int j = 0; j < line.Length; j++)
                {
                    inlineKeyboardButtons[i][j] = new InlineKeyboardButton(line[j].Item1)
                    {
                        CallbackData = line[j].Item2,
                    };
                }
            }

            return inlineKeyboardButtons;
        }
    }
}

