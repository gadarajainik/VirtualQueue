using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using VirtualQueue.Models;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Threading.Tasks;

namespace VirtualQueue.Controllers
{
    public class GuestsController : Controller
    {
        private VQContext db = new VQContext();
        bool mailstatus = false;
        // GET: Guests
        public ActionResult Index()
        {
            return View(db.Guests.Where(x=>x.status=="Waiting").OrderByDescending(x=>x.isVIP).ThenBy(x=>x.entry).ToList());
        }

        // GET: Guests/Details/5
        public ActionResult Details(long? id)
        {
            if (Session["User"] != null)
            {
                if (id == null)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }
                Guest guest = db.Guests.Find(id);
                if (guest == null)
                {
                    return HttpNotFound();
                }
                return View(guest);
            }
            else
                return RedirectToAction("Index","Login");
        }

        // GET: Guests/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Guests/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "bookingID,guestName,email,contact_no,groupSize,persist,isVIP,status,entry,waiting,pending")] Guest guest)
        {
            if (ModelState.IsValid)
            {
                if(guest.contact_no=="" && guest.email=="")
                {
                    ModelState.AddModelError(string.Empty,"You must provide Phone_no or Email to contact you!");
                    return View(guest);
                }
                else
                {
                    guest.status = "Waiting";
                    guest.entry = DateTime.Now;
                    guest.waiting = DateTime.Now;
                    guest.pending = DateTime.Now;
                    db.Guests.Add(guest);
                    db.SaveChanges();
                    return RedirectToAction("Index");
                }
                
            }

            return View(guest);
        }

        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AdmitToPendingLists(long id)
        {
            
            bool smsstatus=false;
            if( Session["User"]!=null)
            {
                Guest g = db.Guests.FirstOrDefault(x=>x.bookingID==id);
                if(g!=null)
                {
                    if (g.email != null && g.email != "")
                    {
                        Debug.WriteLine("Before mail sent.");
                        sendmail(g);
                        //gayo k nai a kai rite check karisu?Apde response debug ma print karye ha okay
                        Debug.WriteLine("After mail sent.");
                    }
                    if (g.contact_no!=null && g.contact_no!="")
                    {
                        if (sendSMS(g)==true)
                        {
                            smsstatus = true;
                            HttpContext.Application["MessageCount"]=(Convert.ToInt32(HttpContext.Application["MessageCount"].ToString())+1).ToString();
                            ProjectConfig c=db.ProjectConfigs.FirstOrDefault(x => x.att_key == "MessageCount");
                            c.att_val = HttpContext.Application["MessageCount"].ToString();
                            db.SaveChanges();
                        }
                    }
                    if (mailstatus == true || smsstatus == true)
                    {
                        g.status = "Pending";
                        g.waiting = DateTime.Now;
                        db.SaveChanges();
                    }
                    else
                    {
                        ViewBag.errmsg = "Failed to send email or sms.Try again.";
                        return View("Error");
                    }
                    //return RedirectToAction("PendingList");
                }
                else
                {
                    ViewBag.errmsg = "Booking ID not found!";
                    return View("Error");
                    //ModelState.AddModelError(string.Empty,"Booking ID not found!");
                }

                return RedirectToAction("PendingList");
            }
            else
            {
                return RedirectToAction("Index","Login");
            }
        }

        [NonAction]
        public bool sendSMS(Guest g)
        {
            try
            {
                TwilioClient.Init(HttpContext.Application["SID"].ToString(),
                                    HttpContext.Application["SecretKey"].ToString());

                var message = MessageResource.Create(
                    body: "It's your turn to enter the " + HttpContext.Application["EventName"].ToString() +              
                    "! Please bring this message to the ticket booth. Enjoy the haunt!",
                    from: new Twilio.Types.PhoneNumber(HttpContext.Application["MobileNo"].ToString()),
                    to: new Twilio.Types.PhoneNumber(g.contact_no)
                );
                
                if(message.Status==MessageResource.StatusEnum.Failed ||
                     message.Status==MessageResource.StatusEnum.Undelivered )
                {
                    Debug.WriteLine("Twilio Error Message:" + message.ErrorMessage);
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch(Exception e)
            {
                Debug.WriteLine(e.Message);
                return false;
            }
        }


        [NonAction]
        public void sendmail(Guest g)
        {
            //var body = "<p><b> Dear {0}, </b></p><br><p>" +
            //"It's your turn to enter the " + HttpContext.Application["EventName"].ToString() +
            //        "! Please bring this message to the ticket booth. Enjoy the haunt!";
            //var message = new MailMessage();
            //string mailid = g.email;


            //message.To.Add(new MailAddress(mailid));


            //message.From = new MailAddress(HttpContext.Application["Email"].ToString());  // replace with valid value
            //message.Subject = " Now its your turn for event " + HttpContext.Application["EventName"].ToString();
            //message.Body = string.Format(body, g.guestName);
            //message.IsBodyHtml = true;

            //using (var smtp = new SmtpClient())
            //{
            //    var credential = new NetworkCredential
            //    {
            //        UserName = HttpContext.Application["Email"].ToString(),  // replace with valid value
            //        Password = HttpContext.Application["EmailPass"].ToString()  // replace with valid value
            //    };
            //    smtp.Credentials = credential;
            //    smtp.Host = "smtp.gmail.com";
            //    smtp.Port = 587;
            //    smtp.EnableSsl = true;
            //    try
            //    {
            //        smtp.Send(message);
            //        return true;
            //    }
            //    catch (Exception es)
            //    {
            //        Debug.WriteLine("ERROR:" + es.Message);
            //        Debug.WriteLine("To:" + mailid + " From:" + HttpContext.Application["Email"].ToString() + " " + HttpContext.Application["EmailPass"].ToString());
            //        return false;
            //    }
            //}


            var client = new SendGridClient(HttpContext.Application["SendGridAPIKey"].ToString());
            var from = new EmailAddress(HttpContext.Application["Email"].ToString(), HttpContext.Application["CompanyName"].ToString());
            var subject = "Alert from "+HttpContext.Application["CompanyName"].ToString()+" (#"+g.bookingID.ToString()+" ) ";
            var to = new EmailAddress(g.email, g.guestName);
            var htmlContent = "";
            var plainTextContent = "Dear "+ g.guestName+",\n" +
                                "It's your turn to enter the " + HttpContext.Application["EventName"].ToString() +
                                "! Please bring this message to the ticket booth. Enjoy the haunt!\nThank You";
            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
            Debug.WriteLine("Before sending mail");
            var response =client.SendEmailAsync(msg);
            Debug.WriteLine("After sending mail");
            Debug.WriteLine("OUTSIDE IF:" + response.Result.StatusCode.ToString());
            if (response.Result.StatusCode.ToString() == "Accepted")
            {
                Debug.WriteLine("After sending mail inside message : "+response.Result.StatusCode.ToString());
                //Debug.WriteLine("After sending mail inside if");
                mailstatus = true;
            }
            Debug.WriteLine("After sending mail outside if");
        }

        public ActionResult PendingList()
        {
            if (Session["User"] != null)
                return View(db.Guests.Where(x => x.status == "Pending").OrderByDescending(x => x.isVIP).ThenBy(x => x.waiting).ToList());
            else
                return RedirectToAction("Index", "Login");
        }

        public ActionResult Arrived(long id)
        {
            if(Session["User"]!=null)
            {

                Guest g = db.Guests.FirstOrDefault(x=>x.bookingID==id);
                if (g != null)
                {
                    if (g.persist == false)
                    {
                        db.Guests.Remove(g);
                     
                    }
                    else
                    {
                        g.status = "Arrived";
                        g.pending = DateTime.Now;

                    }
                    db.SaveChanges();
                    return View(g);
                }
                else
                {
                    ViewBag.errmsg ="Booking ID not found! " ;
                    return View("Error");
                }
            }
            else
            {
                return RedirectToAction("Index","Login");
            }
        }

    }
}
