﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Terraria.Localization;

namespace Terraria.Terraclient.Commands
{
	public static class CheatCommandHandler
	{
		public static int ViewingThisCommandTimer { get; internal set; } = -1;

		public static bool StartTheViewingThisCommandTimer { get; internal set; }

		public static int ColorTimer { get; set; }

		public static string LastChatText { get; internal set; }

		private static string _chatOverlayText;

		public static string ChatOverlayText {
			get {
				if (LastChatText == Main.chatText && ViewingThisCommandTimer != 0) {
					if (ViewingThisCommandTimer > 0)
						ViewingThisCommandTimer--;

					return _chatOverlayText;
				}

				LastChatText = Main.chatText;

				try {
					_chatOverlayText = GetChatOverlayText(LastChatText);
					ViewingThisCommandTimer = -1;

					if (StartTheViewingThisCommandTimer) {
						StartTheViewingThisCommandTimer = false;
						ViewingThisCommandTimer = 80;
					}
				}
				catch (Exception e) {
					CheatCommandUtils.Output(true,
						Language.GetTextValue("CommandErrors.PrettyMessageAboutDeath", e.Message, e.StackTrace), 4);
				}

				return _chatOverlayText;
			}

			internal set => _chatOverlayText = value;
		}

		public static bool ParseCheatCommand(string message) {
			if (!message.StartsWith(".") || message.Length == 1)
				return false;

			List<string> arguments = SplitUpMessage(message);
			string query = arguments[0][1..].ToLower();
			arguments.RemoveAt(0);

			try {
				for (int i = 0; i < MystagogueCommand.CommandList.Count; i++) {
					if (MystagogueCommand.CommandList[i].CommandName == query) {
						List<object> throwUp = Digest(arguments,
							MystagogueCommand.CommandList[i].CommandArgumentDetails[0]);

						if (throwUp.Count > 0 && throwUp[0] is bool)
							return true;

						MystagogueCommand.CommandList[i].CommandActions.ForEach(x => x(throwUp));
						break;
					}

					if (i + 1 == MystagogueCommand.CommandList.Count) {
						CheatCommandUtils.Output(true,
							Language.GetTextValue("CommandErrors.CommandNotRecognized", query), 3);
					}
				}
			}
			catch (Exception e) {
				CheatCommandUtils.Output(true,
					Language.GetTextValue("CommandErrors.PrettyMessageAboutDeath", e.Message, e.StackTrace), 4);
				CheatCommandUtils.Output(false,
					Language.GetTextValue("CommandErrors.PrettyMessageAboutRegisteredText", query,
						string.Join(" ", arguments)));
			}

			return true;
		}

		public static List<object> Digest(List<string> arguments, List<CommandArgument> argumentDetails) {
			if (argumentDetails.Count == 0)
				return arguments.Cast<object>().ToList();

			List<object> polished = new List<object>();

			for (int i = 0; i < argumentDetails.Count; i++) {
				if (arguments.Count == 0 && !argumentDetails[i].MayBeSkipped) {
					if (polished.Count == 0)
						CheatCommandUtils.Output(true, Language.GetTextValue("CommandErrors.CommandRequiresArgs"), 1);
					else {
						List<string> missingArguments = new List<string>();

						for (int j = i; j < argumentDetails.Count; j++)
							missingArguments.Add(argumentDetails[j].ArgumentName);

						CheatCommandUtils.Output(true,
							Language.GetTextValue("CommandErrors.CommandRequiresMoreArgs",
								string.Join(", ", missingArguments)), 1);
					}

					goto OnError;
				}

				if (arguments.Count == 0)
					break;

				if (argumentDetails[i].InputType is CommandArgument.ArgInputType.PositiveIntegerRangeOrText
					    or CommandArgument.ArgInputType.PositiveIntegerRangeOrTextConcatenationUntilNextInt
					    or CommandArgument.ArgInputType.PositiveIntegerRange
				    && !new Regex("\\D").IsMatch(arguments[0])) {
					string parsingString = arguments[0];

					while (parsingString.StartsWith("0"))
						parsingString = parsingString.Remove(0, 1);

					polished.Add(0);

					try {
						if (parsingString.Length > argumentDetails[i].ExpectedInputs[1].ToString()?.Length)
							polished[^1] = argumentDetails[i].ExpectedInputs[1];
						else if (argumentDetails[i].ExpectedInputs[1].ToString()?.Length == 10 &&
						         Convert.ToInt64(parsingString) > 2147483647L)
							polished[^1] = argumentDetails[i].ExpectedInputs[1];
						else if (parsingString.Length > 0)
							polished[^1] = int.Parse(parsingString);
					}
					catch (IndexOutOfRangeException) {
						// ignore
					}

					arguments.RemoveAt(0);
					goto CanNowMoveToNextArgument;
				}

				if (argumentDetails[i].InputType is CommandArgument.ArgInputType.PositiveIntegerRange
				    && new Regex("\\D").IsMatch(arguments[0])) {
					CheatCommandUtils.Output(true,
						Language.GetTextValue("CommandErrors.MustBePositiveInteger", argumentDetails[i].ArgumentName),
						1);
					goto OnError;
				}

				if (argumentDetails[i].InputType is CommandArgument.ArgInputType.CustomText) {
					polished.Add(arguments[0]);
					arguments.RemoveAt(0);
					goto CanNowMoveToNextArgument;
				}

				string query = arguments[0];
				arguments.RemoveAt(0);

				if (argumentDetails[i].InputType is CommandArgument.ArgInputType.TextConcatenationUntilNextInt
					or CommandArgument.ArgInputType.PositiveIntegerRangeOrTextConcatenationUntilNextInt
					or CommandArgument.ArgInputType.CustomTextConcatenationUntilNextInt) {
					while (arguments.Count > 0 && new Regex("\\D").IsMatch(arguments[0])) {
						query += " " + arguments[0];
						arguments.RemoveAt(0);
					}

					if (argumentDetails[i].InputType is CommandArgument.ArgInputType
						.CustomTextConcatenationUntilNextInt) {
						polished.Add(query);
						goto CanNowMoveToNextArgument;
					}
				}

				int l = 0;
				if (argumentDetails[i].InputType
					is CommandArgument.ArgInputType.PositiveIntegerRangeOrText
					or CommandArgument.ArgInputType.PositiveIntegerRangeOrTextConcatenationUntilNextInt) {
					l = 2;
				}

				List<string> matches = new List<string>();
				for (; l < argumentDetails[i].ExpectedInputs.Count; l++) {
					if (((string)argumentDetails[i].ExpectedInputs[l]).StartsWith(query,
						StringComparison.OrdinalIgnoreCase)) {
						matches.Add((string)argumentDetails[i].ExpectedInputs[l]);
					}
				}

				switch (matches.Count) {
					case 0:
						CheatCommandUtils.Output(true,
							Language.GetTextValue("CommandErrors.DidNotMatchAnyOptionsFor",
								argumentDetails[i].ArgumentName), 3);
						goto OnError;

					case > 1:
						foreach (string thing in matches.Where(thing =>
							thing.Equals(query, StringComparison.OrdinalIgnoreCase))) {
							polished.Add(thing);
							matches.Remove(thing);
							CheatCommandUtils.Output(false,
								Language.GetTextValue("CommandWaysideNotifs.MatchedMultipleSelectedOne",
									argumentDetails[i].ArgumentName, thing, string.Join(", ", matches)));
							goto CanNowMoveToNextArgument;
						}

						CheatCommandUtils.Output(true,
							Language.GetTextValue("CommandErrors.SelectedMoreThanOneOptionFor",
								argumentDetails[i].ArgumentName, string.Join(", ", matches)), 2);
						goto OnError;
				}

				polished.Add(matches[0]);
				CanNowMoveToNextArgument: ;
			}

			return polished;

			OnError:
			return new List<object> {false};
		}

		internal static void UpdateColors() {
			ColorTimer++;

			if (ColorTimer != 5)
				return;

			List<string> errorColors = CheatCommandUtils.ErrorColors;
			List<string> safeColors = CheatCommandUtils.NonErrorColors;
			string errorColor = errorColors[^2];
			string safeColor = safeColors[^2];

			errorColors.RemoveAt(errorColors.Count - 2);
			errorColors.Insert(1, errorColor);
			safeColors.RemoveAt(safeColors.Count - 2);
			safeColors.Insert(1, safeColor);

			ColorTimer = 0;
		}

		internal static string GetChatOverlayText(string message) {
			if (message is "" or ".")
				return ".help";

			if (!message.StartsWith("."))
				return "";

			if (message.Contains(" ")) {
				List<string> words = SplitUpMessage(message);
				string command = words[0][1..];
				words.RemoveAt(0);
				IDictionary<string, MystagogueCommand> commandsWithThisName = MystagogueCommand.CommandList
					.Where(
						cmd => cmd.CommandName.Equals(command, StringComparison.OrdinalIgnoreCase))
					.ToDictionary(cmd => cmd.CommandName);

				switch (commandsWithThisName.Count) {
					case < 1:
						return Language.GetTextValue("PredictiveText.NoCommandFound", message.Trim());

					case > 1:
						return Language.GetTextValue("PredictiveText.MoreThanOneCommandWithThisName", message.Trim());
				}

				List<CommandArgument> argumentDetails =
					commandsWithThisName.ElementAt(0).Value.CommandArgumentDetails[0];

				if (argumentDetails.Count == 0)
					return "";

				List<string> finished = new List<string>();

				foreach (CommandArgument detailsOfThis in argumentDetails.TakeWhile(_ => words.Count != 0)) {
					if (detailsOfThis.InputType is CommandArgument.ArgInputType.CustomTextConcatenationUntilNextInt or
						    CommandArgument.ArgInputType.TextConcatenationUntilNextInt ||
					    (detailsOfThis.InputType ==
					     CommandArgument.ArgInputType.PositiveIntegerRangeOrTextConcatenationUntilNextInt &&
					     new Regex("\\D").IsMatch(words[0]))) {
						int indexOfFirstOmitted = words.Count;

						for (int j = 1; j < words.Count; j++) {
							if (new Regex("\\D").IsMatch(words[j]))
								continue;
							indexOfFirstOmitted = j;
							break;
						}

						finished.Add(string.Join(" ", words.GetRange(0, indexOfFirstOmitted)));
						words.RemoveRange(0, indexOfFirstOmitted);
					}
					else {
						finished.Add(words[0]);
						words.RemoveAt(0);
					}
				}

				if (argumentDetails.Count < finished.Count)
					return "";

				if (message.EndsWith(" ") && argumentDetails.Count > finished.Count) {
					string addon;
					switch (argumentDetails[finished.Count].InputType) {
						case CommandArgument.ArgInputType.PositiveIntegerRange:
							if (finished.Count > 0 && (argumentDetails[finished.Count - 1].InputType
								                           is CommandArgument.ArgInputType.TextConcatenationUntilNextInt
							                           || (argumentDetails[finished.Count - 1].InputType
								                               is CommandArgument.ArgInputType
									                               .PositiveIntegerRangeOrTextConcatenationUntilNextInt
							                               && new Regex("\\D").IsMatch(finished[^1])))) {
								List<string> matches = new List<string>();
								int j = 0;

								if (argumentDetails[finished.Count - 1].InputType
									is CommandArgument.ArgInputType
										.PositiveIntegerRangeOrTextConcatenationUntilNextInt) {
									j = 2;
								}

								for (; j < argumentDetails[finished.Count - 1].ExpectedInputs.Count; j++)
									if (((string)argumentDetails[finished.Count - 1].ExpectedInputs[j]).StartsWith(
										finished.Last(), StringComparison.OrdinalIgnoreCase))
										matches.Add((string)argumentDetails[finished.Count - 1].ExpectedInputs[j]);

								switch (matches.Count) {
									case 0:
										return Language.GetTextValue("PredictiveText.NoMatchesForThis", message.Trim());

									case > 1: {
										matches.Sort();
										string output = message.Trim() + matches[0][finished.Last().Length..] +
										                ", " + string.Join(", ",
											                matches.GetRange(1, matches.Count - 1));
										return output[..Math.Min(150, output.Length)] +
										       (output.Length >= 150 ? "..." : "");
									}
								}
							}

							addon = Language.GetTextValue("InputDescriptions.PositiveIntRange",
								argumentDetails[finished.Count].ExpectedInputs[0],
								argumentDetails[finished.Count].ExpectedInputs[1]);
							break;

						case CommandArgument.ArgInputType.Text:
						case CommandArgument.ArgInputType.TextConcatenationUntilNextInt:
							addon = Language.GetTextValue("InputDescriptions.Text");
							break;

						case CommandArgument.ArgInputType.PositiveIntegerRangeOrText:
						case CommandArgument.ArgInputType.PositiveIntegerRangeOrTextConcatenationUntilNextInt:
							addon = Language.GetTextValue("InputDescriptions.PositiveIntRangeOrText",
								argumentDetails[finished.Count].ExpectedInputs[0],
								argumentDetails[finished.Count].ExpectedInputs[1]);
							break;

						case CommandArgument.ArgInputType.CustomText:
						case CommandArgument.ArgInputType.CustomTextConcatenationUntilNextInt:
							addon = Language.GetTextValue("InputDescriptions.CustomText");
							break;

						default:
							throw new ArgumentOutOfRangeException(nameof(message));
					}

					return message.Trim() + " " + argumentDetails[finished.Count].ArgumentName + " " + addon;
				}

				if (argumentDetails[finished.Count - 1].InputType
					    is CommandArgument.ArgInputType.PositiveIntegerRangeOrText
					    or CommandArgument.ArgInputType.PositiveIntegerRangeOrTextConcatenationUntilNextInt
					    or CommandArgument.ArgInputType.PositiveIntegerRange
				    && !new Regex("\\D").IsMatch(finished.Last())
				    || argumentDetails[finished.Count - 1].InputType
					    is CommandArgument.ArgInputType.CustomText
					    or CommandArgument.ArgInputType.CustomTextConcatenationUntilNextInt) {
					return "";
				}

				if (argumentDetails[finished.Count - 1].InputType
					    is CommandArgument.ArgInputType.PositiveIntegerRange
				    && new Regex("\\D").IsMatch(finished.Last())) {
					return message.Trim() + " " + Language.GetTextValue("CommandErrors.MustBePositiveInteger",
						argumentDetails[finished.Count - 1].ArgumentName);
				}

				List<string> listMatches = new List<string>();
				int l = 0;
				if (argumentDetails[finished.Count - 1].InputType
					is CommandArgument.ArgInputType.PositiveIntegerRangeOrText
					or CommandArgument.ArgInputType.PositiveIntegerRangeOrTextConcatenationUntilNextInt) {
					l = 2;
				}

				for (; l < argumentDetails[finished.Count - 1].ExpectedInputs.Count; l++)
					if (((string)argumentDetails[finished.Count - 1].ExpectedInputs[l]).StartsWith(finished.Last(),
						StringComparison.OrdinalIgnoreCase))
						listMatches.Add((string)argumentDetails[finished.Count - 1].ExpectedInputs[l]);
				listMatches.Sort();
				switch (listMatches.Count) {
					case 0:
						return Language.GetTextValue("PredictiveText.NoMatchesForThis", message.Trim());

					case 1:
						return message + listMatches[0][finished.Last().Length..];

					default:
						string output = message + listMatches[0][finished.Last().Length..] + ", " +
						                string.Join(", ", listMatches.GetRange(1, listMatches.Count - 1));
						return output[..Math.Min(150, output.Length)] + (output.Length >= 150 ? "..." : "");
				}
			}

			IDictionary<string, MystagogueCommand> commandsThatStartWithThis = MystagogueCommand.CommandList.Where(
					cmd => $".{cmd.CommandName}"
						.StartsWith(message, StringComparison.OrdinalIgnoreCase))
				.ToDictionary(cmd => cmd.CommandName);

			switch (commandsThatStartWithThis.Count) {
				case 0:
					return Language.GetTextValue("PredictiveText.NoCommandFound", message.Trim());

				case 1:
					return message + ("." + commandsThatStartWithThis.ElementAt(0).Value.CommandName + " " +
					                  commandsThatStartWithThis.ElementAt(0).Value.CommandDescription)
						[message.Length..];

				default:
					IDictionary<string, MystagogueCommand> commandNamesThatDirectlyMatch = MystagogueCommand.CommandList
						.Where(
							cmd => $".{cmd.CommandName}"
								.Equals(message, StringComparison.OrdinalIgnoreCase))
						.ToDictionary(cmd => cmd.CommandName);
					if (commandNamesThatDirectlyMatch.Count > 0) {
						if (commandNamesThatDirectlyMatch.Count > 1)
							return Language.GetTextValue("PredictiveText.MoreThanOneCommandWithThisName",
								message.Trim());
						switch (ViewingThisCommandTimer) {
							case -1:
								StartTheViewingThisCommandTimer = true;
								break;

							case 0:
								return message + ("." + commandNamesThatDirectlyMatch.ElementAt(0).Value.CommandName +
								                  " " +
								                  commandNamesThatDirectlyMatch.ElementAt(0).Value.CommandDescription)[
									message.Length..];
						}
					}

					commandsThatStartWithThis = (from pair
								in commandsThatStartWithThis
							orderby pair.Key
							select pair)
						.ToDictionary(x => x.Key,
							x => x.Value);

					return message +
					       ("." + string.Join(", ", commandsThatStartWithThis.Keys))[message.Length..];
			}
		}

		private static List<string> SplitUpMessage(string message) =>
			new Regex(@"[\""].+?[\""]|[^ ]+").Matches(message).Select(x => x.Value).ToList();
	}
}