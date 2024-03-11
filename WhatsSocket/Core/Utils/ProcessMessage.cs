﻿using Proto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WhatsSocket.Core.Models;
using WhatsSocket.Core.NoSQL;
using WhatsSocket.Core.Signal;
using WhatsSocket.Core.Stores;
using WhatsSocket.Core.WABinary;

namespace WhatsSocket.Core.Utils
{
    public class ProcessMessageUtil
    {
        public static void CleanMessage(WebMessageInfo message, string meId)
        {
            message.Key.RemoteJid = JidUtils.JidNormalizedUser(message.Key.RemoteJid);
            var participant = message.Key.Participant != null ? JidUtils.JidNormalizedUser(message.Key.Participant) : null;
            if (participant != null)
            {
                message.Key.Participant = participant;
            }
            else
            {
                message.Key.ClearParticipant();
            }
            var content = MessageUtil.NormalizeMessageContent(message.Message);

            if (content?.ReactionMessage != null)
            {
                NormalizeKey(message, meId, content.ReactionMessage.Key);
            }
            if (content?.PollUpdateMessage != null)
            {
                NormalizeKey(message, meId, content.PollUpdateMessage.PollCreationMessageKey);
            }
        }

        internal static async Task ProcessMessage(WebMessageInfo message, bool shouldProcessHistoryMsg, AuthenticationCreds? creds, BaseKeyStore keyStore, Delegates.EventEmitter ev)
        {
            var meId = creds.Me.ID;
            var chat = new Chat()
            {
                ID = JidUtils.JidNormalizedUser(GetChatID(message.Key))
            };
            var isRealMessage = IsRealMessage(message, meId);

            if (isRealMessage)
            {
                chat.ConversationTimestamp = message.MessageTimestamp;
                if (ShouldIncrementChatUnread(message))
                {
                    chat.UnreadCount = chat.UnreadCount + 1;
                }
            }
            var content = MessageUtil.NormalizeMessageContent(message.Message);
            // unarchive chat if it's a real message, or someone reacted to our message
            // and we've the unarchive chats setting on
            if ((isRealMessage || content?.ReactionMessage?.Key?.FromMe == true) && creds.AccountSettings.UnarchiveChats)
            {
                chat.Archived = false;
                chat.ReadOnly = false;
            }

            //TODO Impmlement below
            var protocolMsg = content?.ProtocolMessage;
            if (protocolMsg != null)
            {
                switch (content?.ProtocolMessage.Type)
                {
                    case Message.Types.ProtocolMessage.Types.Type.HistorySyncNotification:
                        {
                            var histNotification = content.ProtocolMessage.HistorySyncNotification;
                            var process = shouldProcessHistoryMsg;
                            var isLatest = creds.ProcessedHistoryMessages.Count > 0;

                            if (process)
                            {
                                creds.ProcessedHistoryMessages.Add(new ProcessedHistoryMessage()
                                {
                                    Key = message.Key,
                                    MessageTimestamp = message.MessageTimestamp
                                });
                                ev.Emit(creds);

                                var data = await HistoryUtil.DownloadAndProcessHistorySyncNotification(histNotification);
                            }
                        }
                        break;
                    case Message.Types.ProtocolMessage.Types.Type.AppStateSyncKeyShare:
                        {
                            var newAppStateSyncKeyId = creds.MyAppStateKeyId;
                            var keys = protocolMsg.AppStateSyncKeyShare.Keys;
                            foreach (var item in keys)
                            {
                                var id = item.KeyId.KeyId.ToBase64();
                                var keyData = new AppStateSyncKeyStructure(item.KeyData);
                                keyStore.Set<AppStateSyncKeyStructure>(id, keyData);
                                //repository.Storage.AppStateSyncKeyStore.Set(id, keyData);
                                newAppStateSyncKeyId = id;
                            }
                            creds.MyAppStateKeyId = newAppStateSyncKeyId;
                            ev.Emit(creds);
                        }
                        break;
                    case Message.Types.ProtocolMessage.Types.Type.Revoke:

                        break;
                    case Message.Types.ProtocolMessage.Types.Type.EphemeralSetting:

                        break;
                    case Message.Types.ProtocolMessage.Types.Type.PeerDataOperationRequestMessage:

                        break;

                }
            }
        }

        private static bool ShouldIncrementChatUnread(WebMessageInfo message)
        {
            return (!message.Key.FromMe && message.MessageStubType == WebMessageInfo.Types.StubType.Unknown);
        }

        private static bool IsRealMessage(WebMessageInfo message, string meId)
        {
            var normalizedContent = MessageUtil.NormalizeMessageContent(message.Message);

            var hasSomeContent = MessageUtil.GetContentType(normalizedContent);

            return (normalizedContent != null
                || Constants.REAL_MSG_STUB_TYPES.Contains(message.MessageStubType)
                || (Constants.REAL_MSG_REQ_ME_STUB_TYPES.Contains(message.MessageStubType) & message.MessageStubParameters.Any(x => JidUtils.AreJidsSameUser(meId, x)))
                )
                & hasSomeContent != null
                & normalizedContent?.ProtocolMessage == null
                & normalizedContent?.ReactionMessage == null
                & normalizedContent?.PollUpdateMessage == null;
        }

        private static string GetChatID(MessageKey key)
        {
            if (JidUtils.IsBroadcast(key.RemoteJid)
                & JidUtils.IsJidStatusBroadcast(key.RemoteJid)
                & !key.FromMe)
            {
                return key.Participant;
            }
            return key.RemoteJid;
        }

        private static void NormalizeKey(WebMessageInfo message, string meId, MessageKey msgKey)
        {
            // if the reaction is from another user
            // we've to correctly map the key to this user's perspective
            if (!message.Key.FromMe)
            {
                // if the sender believed the message being reacted to is not from them
                // we've to correct the key to be from them, or some other participant
                msgKey.FromMe = msgKey.FromMe == false ? JidUtils.AreJidsSameUser(msgKey.Participant ?? msgKey.RemoteJid, meId)
                // if the message being reacted to, was from them
                // fromMe automatically becomes false
                : false;
                // set the remoteJid to being the same as the chat the message came from
                msgKey.RemoteJid = message.Key.RemoteJid;
                // set participant of the message
                msgKey.Participant = msgKey.Participant ?? msgKey.RemoteJid;
            }
        }
    }
}