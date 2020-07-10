﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlTools.Hosting.Contracts;
using Microsoft.SqlTools.Hosting.Protocol.Contracts;
using Microsoft.SqlTools.Hosting.Protocol.Serializers;
using Microsoft.SqlTools.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.SqlTools.Hosting.Protocol
{
    public class MessageWriter
    {
        #region Private Fields

        private Stream outputStream;
        private IMessageSerializer messageSerializer;

        private JsonSerializer contentSerializer = 
            JsonSerializer.Create(
                Constants.JsonSerializerSettings);

        #endregion

        #region Constructors

        public MessageWriter(
            Stream outputStream,
            IMessageSerializer messageSerializer)
        {
            Validate.IsNotNull("streamWriter", outputStream);
            Validate.IsNotNull("messageSerializer", messageSerializer);

            this.outputStream = outputStream;
            this.messageSerializer = messageSerializer;
        }

        #endregion

        #region Public Methods

        // TODO: This method should be made protected or private

        public async Task WriteMessage(Message messageToWrite)
        {
            Validate.IsNotNull("messageToWrite", messageToWrite);

            // Serialize the message
            JObject messageObject =
                this.messageSerializer.SerializeMessage(
                    messageToWrite);

            // Log the JSON representation of the message
            string logMessage =
                $"Sending message of id[{messageToWrite.Id}], of type[{messageToWrite.MessageType}] and method[{messageToWrite.Method}]";
            Logger.Write(TraceEventType.Verbose, logMessage);

            string serializedMessage =
                JsonConvert.SerializeObject(
                    messageObject,
                    Constants.JsonSerializerSettings);

            byte[] messageBytes = Encoding.UTF8.GetBytes(serializedMessage);
            byte[] headerBytes = Encoding.ASCII.GetBytes(string.Format(Constants.ContentLengthFormatString, messageBytes.Length));

            var buffer = new byte[messageBytes.Length + headerBytes.Length];
            Array.Copy(headerBytes, buffer, headerBytes.Length);
            Array.Copy(messageBytes, 0, buffer, headerBytes.Length, messageBytes.Length);

            await this.outputStream.WriteAsync(buffer, 0, buffer.Length);
            await this.outputStream.FlushAsync();
        }

        public async Task WriteRequest<TParams, TResult>(
            RequestType<TParams, TResult> requestType, 
            TParams requestParams,
            int requestId)
        {
            // Allow null content
            JToken contentObject =
                requestParams != null ?
                    JToken.FromObject(requestParams, contentSerializer) :
                    null;

            await this.WriteMessage(
                Message.Request(
                    requestId.ToString(), 
                    requestType.MethodName,
                    contentObject));
        }

        public async Task WriteResponse<TResult>(TResult resultContent, string method, string requestId)
        {
            // Allow null content
            JToken contentObject =
                resultContent != null ?
                    JToken.FromObject(resultContent, contentSerializer) :
                    null;

            await this.WriteMessage(
                Message.Response(
                    requestId,
                    method,
                    contentObject));
        }

        public async Task WriteEvent<TParams>(EventType<TParams> eventType, TParams eventParams)
        {
            // Allow null content
            JToken contentObject =
                eventParams != null ?
                    JToken.FromObject(eventParams, contentSerializer) :
                    null;

            await this.WriteMessage(
                Message.Event(
                    eventType.MethodName,
                    contentObject));
        }

        public async Task WriteError(string method, string requestId, Error error)
        {
            JToken contentObject = JToken.FromObject(error, contentSerializer);
            await this.WriteMessage(Message.ResponseError(requestId, method, contentObject));
        }
        
        #endregion
    }
}
