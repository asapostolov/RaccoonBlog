﻿using System;
using System.Configuration;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Web.Mvc;
using Joel.Net;
using Newtonsoft.Json;
using RavenDbBlog.Common;
using RavenDbBlog.Core.Models;
using RavenDbBlog.Domain;
using RavenDbBlog.Infrastructure;
using RavenDbBlog.ViewModels;
using RazorEngine;

namespace RavenDbBlog.Commands
{
    public class AddCommentCommand : ICommand
    {
        private readonly CommentInput _commentInput;
        private readonly int _postId;

        public AddCommentCommand(CommentInput commentInput, int postId)
        {
            _commentInput = commentInput;
            _postId = postId;
        }

        public void Execute()
        {
            var session = RavenActionFilterAttribute.DocumentStore.OpenSession();
            var postAndComments = new PostReader(session).GetPostAndComments(_postId);
            var post = postAndComments.Item1;
            post.CommentsCount++;

            var comments = postAndComments.Item2;
            var comment = new CommentsCollection.Comment
            {
                Author = _commentInput.Name,
                Body = _commentInput.Body,
                CreatedAt = DateTimeOffset.Now,  // TODO: Time zone of the client.
                Email = _commentInput.Email,
                Important = false,
                Url = _commentInput.Url,
                IsSpam = CheckForSpam(_commentInput),
            };

            if (comment.IsSpam)
            {
                comments.Spam.Add(comment);
            }
            else
            {
                comments.Comments.Add(comment);
            }

            session.SaveChanges();

            SendEmail(comment);
        }

        private void SendEmail(CommentsCollection.Comment comment)
        {
            var message = new MailMessage();

            var commentsMederatorEmails = ConfigurationSettings.AppSettings["CommentsMederatorEmails"];
            commentsMederatorEmails
                .Split(';')
                .Select(x => new MailAddress(x.Trim()))
                .ForEach(email => message.To.Add(email));

            var blogName = ConfigurationSettings.AppSettings["blogName"];
            message.Subject = string.Format("A new comment on {0} blog", blogName);

            var vm = new NewCommentEmailViewModel
                         {
                             Author = comment.Author,
                             Body = MvcHtmlString.Create(comment.Body),
                             CreatedAt = comment.CreatedAt,
                             Email = comment.Email,
                             Url = comment.Url,
                         };
            message.Body = Razor.Run(vm, "NewComment");

            message.IsBodyHtml = true;
            new SendEmailCommand(message).Execute();
        }

        public bool CheckForSpam(CommentInput comment)
        {
            //Create a new instance of the Akismet API and verify your key is valid.
            string blog = "http://" + ConfigurationSettings.AppSettings["MainUrl"];
            var api = new Akismet(ConfigurationSettings.AppSettings["AkismetKey"], blog, _commentInput.Request.UserAgent);
            if (!api.VerifyKey()) throw new Exception("Akismet API key invalid.");

            var akismetComment = new AkismetComment
            {
                Blog = blog,
                UserIp = _commentInput.Request.UserHostAddress,
                UserAgent = _commentInput.Request.UserAgent,
                CommentContent = comment.Body,
                CommentType = "comment",
                CommentAuthor = comment.Name,
                CommentAuthorEmail = comment.Email,
                CommentAuthorUrl = comment.Url,
            };

            //Check if Akismet thinks this comment is spam. Returns TRUE if spam.
            return api.CommentCheck(akismetComment);
        }
    }
}