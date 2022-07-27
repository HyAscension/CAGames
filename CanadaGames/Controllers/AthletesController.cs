using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CanadaGames.Data;
using CanadaGames.Models;
using CanadaGames.Utilities;
using CanadaGames.ViewModels;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using Microsoft.AspNetCore.Http.Features;

namespace CanadaGames.Controllers
{
    [Authorize]
    public class AthletesController : Controller
    {
        private readonly CanadaGamesContext _context;

        public AthletesController(CanadaGamesContext context)
        {
            _context = context;
        }

        // GET: Athletes
        public async Task<IActionResult> Index(string AthleteSearch, string MediaInfoSearch, string sortDirectionCheck, string sortFieldID, int? ContingentID, int? CoachID, int? GenderID, int? SportID, int? page, int? pageSizeID, string actionButton, string sortDirection = "asc", string sortField = "Athlete")
        {
            //Clear the sort/filter/paging URL Cookie for Controller
            CookieHelper.CookieSet(HttpContext, ControllerName() + "URL", "", -1);

            //Change colour of the button when filtering by setting this default
            ViewData["Filtering"] = "btn-outline-secondary";
            //and then in each "test" for filtering, add ViewData["Filtering"] = "btn-danger" if true;

            //NOTE: make sure this array has matching values to the column headings
            string[] sortOptions = new[] { "Athlete", "Cont.", "Age", "Sports" };

            PopulateDropDownLists();

            //Start with Includes but make sure your expression returns an
            //IQueryable<Athlete> so we can add filter and sort 
            //options later.
            var athletes = from a in _context.Athletes
                .Include(a => a.Coach)
                .Include(d => d.AthleteDocuments)
                .Include(p => p.AthleteThumbnail)
                .Include(a => a.Contingent)
                .Include(a => a.Gender)
                .Include(a => a.Sport)
                .Include(a => a.AthleteSports).ThenInclude(s=>s.Sport)
                select a;

            //Add as many filters as needed
            if (ContingentID.HasValue)
            {
                athletes = athletes.Where(p => p.ContingentID == ContingentID);
                ViewData["Filtering"] = " show";
            }
            if (SportID.HasValue)
            {
                athletes = athletes.Where(p => p.SportID == SportID);
                ViewData["Filtering"] = " show";
            }
            if (GenderID.HasValue)
            {
                athletes = athletes.Where(p => p.GenderID == GenderID);
                ViewData["Filtering"] = " show";
            }
            if (CoachID.HasValue)
            {
                athletes = athletes.Where(p => p.CoachID == CoachID);
                ViewData["Filtering"] = " show";
            }
            if (!String.IsNullOrEmpty(AthleteSearch))
            {
                athletes = athletes.Where(p => p.LastName.ToUpper().Contains(AthleteSearch.ToUpper())
                                       || p.FirstName.ToUpper().Contains(AthleteSearch.ToUpper()));
                ViewData["Filtering"] = " show";
            }
            if (!String.IsNullOrEmpty(MediaInfoSearch))
            {
                athletes = athletes.Where(p => p.MediaInfo.ToUpper().Contains(MediaInfoSearch.ToUpper()));
                ViewData["Filtering"] = " show";
            }

            //Before we sort, see if we have called for a change of filtering or sorting
            if (!String.IsNullOrEmpty(actionButton)) //Form Submitted!
            {
                page = 1;//Reset page to start

                if (sortOptions.Contains(actionButton))//Change of sort is requested
                {
                    if (actionButton == sortField) //Reverse order on same field
                    {
                        sortDirection = sortDirection == "asc" ? "desc" : "asc";
                    }
                    sortField = actionButton;//Sort by the button clicked
                }
                else //Sort by the controls in the filter area
                {
                    sortDirection = String.IsNullOrEmpty(sortDirectionCheck) ? "asc" : "desc";
                    sortField = sortFieldID;
                }
            }
            //Now we know which field and direction to sort by
            if (sortField == "Cont.")
            {
                if (sortDirection == "asc")
                {
                    athletes = athletes
                        .OrderBy(p => p.Contingent.Name)
                        .ThenByDescending(p => p.LastName)
                        .ThenByDescending(p => p.FirstName);
                }
                else
                {
                    athletes = athletes
                        .OrderByDescending(p => p.Contingent.Name)
                        .ThenByDescending(p => p.LastName)
                        .ThenByDescending(p => p.FirstName);
                }
            }
            else if (sortField == "Age")
            {
                if (sortDirection == "asc")
                {
                    athletes = athletes
                        .OrderByDescending(p => p.DOB)
                        .ThenByDescending(p => p.LastName)
                        .ThenByDescending(p => p.FirstName);
                }
                else
                {
                    athletes = athletes
                        .OrderBy(p => p.DOB)
                        .ThenBy(p => p.LastName)
                        .ThenBy(p => p.FirstName);
                }
            }
            else if (sortField == "Sports")
            {
                if (sortDirection == "asc")
                {
                    athletes = athletes
                        .OrderBy(p => p.Sport.Name)
                        .ThenBy(p => p.LastName)
                        .ThenBy(p => p.FirstName);
                }
                else
                {
                    athletes = athletes
                        .OrderByDescending(p => p.Sport.Name)
                        .ThenByDescending(p => p.LastName)
                        .ThenByDescending(p => p.FirstName);
                }
            }
            else //Sorting by Athlete Name
            {
                if (sortDirection == "asc")
                {
                    athletes = athletes
                        .OrderBy(p => p.LastName)
                        .ThenBy(p => p.FirstName);
                }
                else
                {
                    athletes = athletes
                        .OrderByDescending(p => p.LastName)
                        .ThenByDescending(p => p.FirstName);
                }
            }
            //Set sort for next time
            ViewData["sortField"] = sortField;
            ViewData["sortDirection"] = sortDirection;

            //SelectList for Sorting Options
            ViewBag.sortFieldID = new SelectList(sortOptions, sortField.ToString());

            //Handle Paging
            int pageSize = PageSizeHelper.SetPageSize(HttpContext, pageSizeID, ControllerName());
            ViewData["pageSizeID"] = PageSizeHelper.PageSizeList(pageSize);

            var pagedData = await PaginatedList<Athlete>.CreateAsync(athletes.AsNoTracking(), page ?? 1, pageSize);

            return View(pagedData);
        }

        // GET: Athletes/Details/5
        [Authorize(Roles = "Admin, Supervisor, Staff")]
        public async Task<IActionResult> Details(int? id)
        {
            //URL with the last filter, sort and page parameters for this controller
            ViewDataReturnURL();

            if (id == null)
            {
                return NotFound();
            }

            var athlete = await _context.Athletes
                .Include(a => a.Coach)
                .Include(p => p.AthletePhoto)
                .Include(p => p.AthleteDocuments)
                .Include(a => a.Contingent)
                .Include(a => a.Gender)
                .Include(a => a.Sport)
                .Include(a => a.Hometown).ThenInclude(a => a.Contingent)
                .Include(a => a.AthleteSports).ThenInclude(s => s.Sport)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ID == id);
            if (athlete == null)
            {
                return NotFound();
            }

            return View(athlete);
        }

        // GET: Athletes/Create
        [Authorize(Roles = "Admin, Supervisor, Staff")]
        public IActionResult Create()
        {
            //URL with the last filter, sort and page parameters for this controller
            ViewDataReturnURL();

            //Add all (unchecked) Conditions to ViewBag
            var athlete = new Athlete();
            PopulateAssignedSportData(athlete);
            PopulateDropDownLists();
            return View();
        }

        // POST: Athletes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [Authorize(Roles = "Admin, Supervisor, Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FirstName,MiddleName,LastName,AthleteCode,HometownID,DOB,Height,Weight,YearsInSport,Affiliation,Goals,MediaInfo,ContingentID,SportID,GenderID,CoachID")] Athlete athlete, string[] selectedOptions, IFormFile thePicture, List<IFormFile> theFiles)
        {
            //URL with the last filter, sort and page parameters for this controller
            ViewDataReturnURL();

            try
            {
                //Add the selected conditions
                if (selectedOptions != null)
                {
                    //For the BONUS we do not want to add the same "other" sport as
                    //their main sport.  This will remove that option.
                    selectedOptions = selectedOptions.Where(s => s != athlete.SportID.ToString()).ToArray();
                    foreach (var sport in selectedOptions)
                    {
                        var sportToAdd = new AthleteSport { AthleteID = athlete.ID, SportID = int.Parse(sport) };
                        athlete.AthleteSports.Add(sportToAdd);
                    }
                }
                if (ModelState.IsValid)
                {
                    await AddPicture(athlete, thePicture);
                    await AddDocumentsAsync(athlete, theFiles);
                    _context.Add(athlete);
                    await _context.SaveChangesAsync();
                    return RedirectToAction("Index", "AthletePlacements", new { AthleteID = athlete.ID });
                }
            }
            catch (RetryLimitExceededException /* dex */)
            {
                ModelState.AddModelError("", "Unable to save changes after multiple attempts. Try again, and if the problem persists, see your system administrator.");
            }
            catch (DbUpdateException dex)
            {
                if (dex.GetBaseException().Message.Contains("UNIQUE constraint failed: Athletes.AthleteCode"))
                {
                    ModelState.AddModelError("AthleteCode", "Unable to save changes. Remember, you cannot duplicate Athlete Codes.");
                }
                else
                {
                    ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
                }
            }

            PopulateAssignedSportData(athlete);
            PopulateDropDownLists(athlete);
            return View(athlete);
        }

        // GET: Athletes/Edit/5
        [Authorize(Roles = "Admin, Supervisor")]
        public async Task<IActionResult> Edit(int? id)
        {
            //URL with the last filter, sort and page parameters for this controller
            ViewDataReturnURL();

            if (id == null)
            {
                return NotFound();
            }

            var athlete = await _context.Athletes
                .Include(p => p.AthletePhoto)
                .Include(d => d.AthleteDocuments)
                .Include(a => a.AthleteSports).ThenInclude(s => s.Sport)
                .FirstOrDefaultAsync(p => p.ID == id);

            if (athlete == null)
            {
                return NotFound();
            }

            PopulateAssignedSportData(athlete);
            PopulateDropDownLists(athlete);
            return View(athlete);
        }

        // POST: Athletes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [Authorize(Roles = "Admin, Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, int SportID, string[] selectedOptions, Byte[] RowVersion, string chkRemoveImage, IFormFile thePicture, List<IFormFile> theFiles)
        {
            //URL with the last filter, sort and page parameters for this controller
            ViewDataReturnURL();


            //We added the Foreign Key (SportID) parameter so we can eliminate it from being added as an
            //alternate sport for the Athlete.
            //For the BONUS we need to leave out the Athletes' main sport if it is checked
            selectedOptions = selectedOptions.Where(s => s != SportID.ToString()).ToArray();

            //Go get the athlete to update
            var athleteToUpdate = await _context.Athletes
                .Include(p => p.AthletePhoto)
                .Include(d => d.AthleteDocuments)
                .Include(a => a.AthleteSports).ThenInclude(s => s.Sport)
                .FirstOrDefaultAsync(p => p.ID == id);

            //Check that you got it or exit with a not found error
            if (athleteToUpdate == null)
            {
                return NotFound();
            }

            UpdateAthleteSports(selectedOptions, athleteToUpdate);

            //Put the original RowVersion value in the OriginalValues collection for the entity
            _context.Entry(athleteToUpdate).Property("RowVersion").OriginalValue = RowVersion;

            //Try updating it with the values posted
            if (await TryUpdateModelAsync<Athlete>(athleteToUpdate, "",
                p => p.AthleteCode, p => p.FirstName, p => p.MiddleName, p => p.LastName, p => p.DOB, p => p.HometownID,
                p => p.Height, p => p.Weight, p => p.YearsInSport, p => p.Affiliation, p => p.Goals,
                p => p.MediaInfo, p => p.ContingentID, p => p.GenderID, p => p.CoachID, p => p.SportID))
            {
                try
                {
                    //For the image
                    if (chkRemoveImage != null)
                    {
                        //If we are just deleting the two versions of the photo, we need to make sure the Change Tracker knows
                        //about them both so go get the Thumbnail since we did not include it.
                        athleteToUpdate.AthleteThumbnail = _context.AthleteThumbnails.Where(p => p.AthleteID == athleteToUpdate.ID).FirstOrDefault();
                        //Then, setting them to null will cause them to be deleted from the database.
                        athleteToUpdate.AthletePhoto = null;
                        athleteToUpdate.AthleteThumbnail = null;
                    }
                    else
                    {
                        await AddPicture(athleteToUpdate, thePicture);
                    }
                    await AddDocumentsAsync(athleteToUpdate, theFiles);
                    await _context.SaveChangesAsync();
                    return RedirectToAction("Index", "AthletePlacements", new { AthleteID = athleteToUpdate.ID });
                }
                catch (RetryLimitExceededException /* dex */)
                {
                    ModelState.AddModelError("", "Unable to save changes after multiple attempts. Try again, and if the problem persists, see your system administrator.");
                }
                catch (DbUpdateConcurrencyException ex)// Added for concurrency
                {
                    var exceptionEntry = ex.Entries.Single();
                    var clientValues = (Athlete)exceptionEntry.Entity;
                    var databaseEntry = exceptionEntry.GetDatabaseValues();
                    if (databaseEntry == null)
                    {
                        ModelState.AddModelError("",
                            "Unable to save changes. The Athlete was deleted by another user.");
                    }
                    else
                    {
                        var databaseValues = (Athlete)databaseEntry.ToObject();
                        if (databaseValues.FirstName != clientValues.FirstName)
                            ModelState.AddModelError("FirstName", "Current value: "
                                + databaseValues.FirstName);
                        if (databaseValues.MiddleName != clientValues.MiddleName)
                            ModelState.AddModelError("MiddleName", "Current value: "
                                + databaseValues.MiddleName);
                        if (databaseValues.LastName != clientValues.LastName)
                            ModelState.AddModelError("LastName", "Current value: "
                                + databaseValues.LastName);
                        if (databaseValues.AthleteCode != clientValues.AthleteCode)
                            ModelState.AddModelError("AthleteCode", "Current value: "
                                + databaseValues.AthleteCode);
                        if (databaseValues.DOB != clientValues.DOB)
                            ModelState.AddModelError("DOB", "Current value: "
                                + String.Format("{0:d}", databaseValues.DOB));
                        if (databaseValues.Height != clientValues.Height)
                            ModelState.AddModelError("Height", "Current value: "
                                + databaseValues.Height);

                        if (databaseValues.Weight != clientValues.Weight)
                            ModelState.AddModelError("Weight", "Current value: "
                                + databaseValues.Weight);

                        if (databaseValues.YearsInSport != clientValues.YearsInSport)
                            ModelState.AddModelError("YearsInSport", "Current value: "
                                + databaseValues.YearsInSport);

                        if (databaseValues.Affiliation != clientValues.Affiliation)
                            ModelState.AddModelError("Affiliation", "Current value: "
                                + databaseValues.Affiliation);

                        if (databaseValues.Goals != clientValues.Goals)
                            ModelState.AddModelError("Goals", "Current value: "
                                + databaseValues.Goals);

                        if (databaseValues.MediaInfo != clientValues.MediaInfo)
                            ModelState.AddModelError("MediaInfo", "Current value: "
                                + databaseValues.MediaInfo);

                        //For the foreign key, we need to go to the database to get the information to show
                        if (databaseValues.ContingentID != clientValues.ContingentID)
                        {
                            Contingent databaseContingent = await _context.Contingents.SingleOrDefaultAsync(i => i.ID == databaseValues.ContingentID);
                            ModelState.AddModelError("ContingentID", $"Current value: {databaseContingent?.Name}");
                        }
                        if (databaseValues.SportID != clientValues.SportID)
                        {
                            Sport databaseSport = await _context.Sports.SingleOrDefaultAsync(i => i.ID == databaseValues.SportID);
                            ModelState.AddModelError("SportID", $"Current value: {databaseSport?.Name}");
                        }
                        if (databaseValues.GenderID != clientValues.GenderID)
                        {
                            Gender databaseGender = await _context.Genders.SingleOrDefaultAsync(i => i.ID == databaseValues.GenderID);
                            ModelState.AddModelError("GenderID", $"Current value: {databaseGender?.Name}");
                        }
                        //A little extra work for the nullable foreign keys.  No sense going to the database and asking for something
                        //we already know is not there.
                        if (databaseValues.CoachID != clientValues.CoachID)
                        {
                            if (databaseValues.CoachID.HasValue)
                            {
                                Coach databaseCoach = await _context.Coaches.SingleOrDefaultAsync(i => i.ID == databaseValues.CoachID);
                                ModelState.AddModelError("CoachID", $"Current value: {databaseCoach?.FullName}");
                            }
                            else

                            {
                                ModelState.AddModelError("CoachID", $"Current value: None");
                            }
                        }
                        if (databaseValues.HometownID != clientValues.HometownID)
                        {
                            if (databaseValues.HometownID.HasValue)
                            {
                                Hometown databaseHometown = await _context.Hometowns.SingleOrDefaultAsync(i => i.ID == databaseValues.HometownID);
                                ModelState.AddModelError("HometownID", $"Current value: {databaseHometown?.HometownContingent}");
                            }
                            else

                            {
                                ModelState.AddModelError("HometownID", $"Current value: None");
                            }
                        }
                        ModelState.AddModelError(string.Empty, "The record you attempted to edit "
                                + "was modified by another user after you received your values. The "
                                + "edit operation was canceled and the current values in the database "
                                + "have been displayed. If you still want to save your version of this record, click "
                                + "the Save button again. Otherwise click the 'Back to List' hyperlink.");
                        athleteToUpdate.RowVersion = (byte[])databaseValues.RowVersion;
                        ModelState.Remove("RowVersion");
                    }
                }
                catch (DbUpdateException dex)
                {
                    if (dex.GetBaseException().Message.Contains("UNIQUE constraint failed: Athletes.AthleteCode"))
                    {
                        ModelState.AddModelError("AthleteCode", "Unable to save changes. Remember, you cannot duplicate Athlete Codes.");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
                    }
                }
            }

            PopulateAssignedSportData(athleteToUpdate);
            PopulateDropDownLists(athleteToUpdate);
            return View(athleteToUpdate);
        }

        // GET: Athletes/Delete/5
        [Authorize(Roles = "Admin, Supervisor")]
        public async Task<IActionResult> Delete(int? id)
        {
            //URL with the last filter, sort and page parameters for this controller
            ViewDataReturnURL();

            if (id == null)
            {
                return NotFound();
            }

            var athlete = await _context.Athletes
                .Include(a => a.Coach)
                .Include(a => a.Contingent)
                .Include(a => a.Gender)
                .Include(a => a.Sport)
                .Include(a => a.Hometown).ThenInclude(a => a.Contingent)
                .Include(a => a.AthleteSports).ThenInclude(s => s.Sport)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ID == id);
            if (athlete == null)
            {
                return NotFound();
            }

            return View(athlete);
        }

        // POST: Athletes/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin, Supervisor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            //URL with the last filter, sort and page parameters for this controller
            ViewDataReturnURL();

            var athlete = await _context.Athletes
                .Include(a => a.Coach)
                .Include(a => a.Contingent)
                .Include(a => a.Gender)
                .Include(a => a.Sport)
                .Include(a => a.Hometown).ThenInclude(a => a.Contingent)
                .Include(a => a.AthleteSports).ThenInclude(s => s.Sport)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ID == id);
            try
            {
                _context.Athletes.Remove(athlete);
                await _context.SaveChangesAsync();
                return Redirect(ViewData["returnURL"].ToString());

            }
            catch (DbUpdateException)
            {
                //Note: there is really no reason a delete should fail if you can "talk" to the database.
                ModelState.AddModelError("", "Unable to delete record. Try again, and if the problem persists see your system administrator.");
            }
            return View(athlete);

        }

        public IActionResult PlacementReport()
        {
            var sumQ = _context.Placements
                .Include(a => a.Athlete)
                .GroupBy(a => new { a.AthleteID, a.Athlete.Contingent.Code, a.Athlete.LastName, a.Athlete.FirstName, a.Athlete.MiddleName })
                .Select(grp => new PlacementReport
                {
                    ID = grp.Key.AthleteID,
                    ContingentCode = grp.Key.Code,
                    FirstName = grp.Key.FirstName,
                    MiddleName = grp.Key.MiddleName,
                    LastName = grp.Key.LastName,
                    AvgPlace = Math.Round((grp.Average(p => p.Place)), 2),
                    MaxPlace = grp.Min(p => p.Place),
                    MinPlace = grp.Max(p => p.Place),
                    NumberOfEvents = grp.Count()
                });

            return View(sumQ.AsNoTracking().ToList());
        }

        [Authorize(Roles = "Admin")]
        public IActionResult DownloadPlacements()
        {
            var plcmt = from a in _context.Placements
                        .Include(a => a.Athlete)
                        .ThenInclude(p => p.Contingent)
                        .GroupBy(a => new { a.AthleteID, a.Athlete.Contingent.Code, a.Athlete.LastName, a.Athlete.FirstName, a.Athlete.MiddleName, a.Athlete.MediaInfo})
                        .Select(grp => new PlacementReport
                        {
                            ID = grp.Key.AthleteID,
                            ContingentCode = grp.Key.Code,
                            FirstName = grp.Key.FirstName,
                            MiddleName = grp.Key.MiddleName,
                            LastName = grp.Key.LastName,
                            AvgPlace = Math.Round((grp.Average(p => p.Place)), 2),
                            MaxPlace = grp.Min(p => p.Place),
                            MinPlace = grp.Max(p => p.Place),
                            MediaInfo = grp.Key.MediaInfo,
                            NumberOfEvents = grp.Count()
                        })
                        orderby a.FirstName descending
                        select new
                        {
                            Athlete = a.FullName,
                            Contingent = a.ContingentCode,
                            Average_Place = a.AvgPlace,
                            Highest_Place = a.MaxPlace,
                            Lowest_Place = a.MinPlace,
                            Number_Of_Events = a.NumberOfEvents,
                            Comments = a.MediaInfo
                        };
            int numRows = plcmt.Count();

            //Create a new spreadsheet
            if (numRows > 0)
            {
                using (ExcelPackage excel = new ExcelPackage())
                {
                    var workSheet = excel.Workbook.Worksheets.Add("Placements");

                    workSheet.Cells[3, 1].LoadFromCollection(plcmt, true);
                    workSheet.Cells[4, 1, numRows + 3, 2].Style.Font.Bold = true;

                    using (ExcelRange totalAthletes = workSheet.Cells[numRows + 4, 1])//
                    {
                        totalAthletes.Formula = $"SUMPRODUCT(--(FREQUENCY(MATCH({workSheet.Cells[4, 1].Address}:{workSheet.Cells[numRows + 3, 1].Address},{workSheet.Cells[4, 1].Address}:{workSheet.Cells[numRows + 3, 1].Address}, 0), ROW({workSheet.Cells[4, 1].Address}:{workSheet.Cells[numRows + 3, 1].Address}) - ROW({workSheet.Cells[4, 1].Address}) + 1) > 0))";
                        totalAthletes.Style.Font.Bold = true;
                    }

                    using (ExcelRange totalEvents = workSheet.Cells[numRows + 4, 6])//
                    {
                        totalEvents.Formula = $"MAX({workSheet.Cells[4, 6].Address}:{workSheet.Cells[numRows + 3, 6].Address})";
                        totalEvents.Style.Font.Bold = true;
                    }

                    //Style của background and heading
                    using (ExcelRange headings = workSheet.Cells[3, 1, 3, 7])
                    {
                        headings.Style.Font.Bold = true;
                        var fill = headings.Style.Fill;
                        fill.PatternType = ExcelFillStyle.Solid;
                        fill.BackgroundColor.SetColor(Color.LightBlue);
                    }
                    workSheet.Cells.AutoFitColumns();

                    workSheet.Cells[1, 1].Value = "Placement Report";
                    using (ExcelRange Rng = workSheet.Cells[1, 1, 1, 6])
                    {
                        Rng.Merge = true;
                        Rng.Style.Font.Bold = true;
                        Rng.Style.Font.Size = 18;
                        Rng.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    }

                    //Adjust local times
                    DateTime utcDate = DateTime.UtcNow;
                    TimeZoneInfo esTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                    DateTime localDate = TimeZoneInfo.ConvertTimeFromUtc(utcDate, esTimeZone);
                    using (ExcelRange Rng = workSheet.Cells[2, 6])
                    {
                        Rng.Value = "Created: " + localDate.ToShortTimeString() + " on " +
                            localDate.ToShortDateString();
                        Rng.Style.Font.Bold = true;
                        Rng.Style.Font.Size = 12;
                        Rng.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    }

                    //Download Excel
                    var syncIOFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
                    if (syncIOFeature != null)
                    {
                        syncIOFeature.AllowSynchronousIO = true;
                        using (var memoryStream = new MemoryStream())
                        {
                            Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                            Response.Headers["content-disposition"] = "attachment;  filename=Placements.xlsx";
                            excel.SaveAs(memoryStream);
                            memoryStream.WriteTo(Response.Body);
                        }
                    }
                    else
                    {
                        try
                        {
                            Byte[] theData = excel.GetAsByteArray();
                            string filename = "Placements.xlsx";
                            string mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                            return File(theData, mimeType, filename);
                        }
                        catch (Exception)
                        {
                            return NotFound();
                        }
                    }
                }
            }
            return NotFound();
        }

        private void PopulateAssignedSportData(Athlete athlete)
        {
            //For this to work, you must have Included the AthleteSports 
            //in the Athlete
            //Note: for the BONUS, we won't make a checkbox for their main Sport
            var allOptions = _context.Sports.Where(s=>s.ID !=athlete.SportID);
            var currentOptionIDs = new HashSet<int>(athlete.AthleteSports.Select(b => b.SportID));
            var checkBoxes = new List<CheckOptionVM>();
            foreach (var option in allOptions)
            {
                checkBoxes.Add(new CheckOptionVM
                {
                    ID = option.ID,
                    DisplayText = option.Name,
                    Assigned = currentOptionIDs.Contains(option.ID)
                });
            }
            ViewData["SportOptions"] = checkBoxes;
        }
        private void UpdateAthleteSports(string[] selectedOptions, Athlete athleteToUpdate)
        {
            //This is an alternate approach to what I demonstrated in class.
            //Instetad of trying to follow the logic in the tutorial, we are
            //just clearing them out and adding the selected ones back in.
            athleteToUpdate.AthleteSports = new List<AthleteSport>();
            if (selectedOptions == null)
            {
                return;
            }
            else
            {
                foreach (string s in selectedOptions)
                {
                    athleteToUpdate.AthleteSports.Add(new AthleteSport
                    {
                        AthleteID = athleteToUpdate.ID,
                        SportID = Convert.ToInt32(s)
                    });
                }
            }
        }

        private async Task AddPicture(Athlete athlete, IFormFile thePicture)
        {
            //Get the picture and save it with the Athlete (2 sizes)
            if (thePicture != null)
            {
                string mimeType = thePicture.ContentType;
                long fileLength = thePicture.Length;
                if (!(mimeType == "" || fileLength == 0))//Looks like we have a file!!!
                {
                    if (mimeType.Contains("image"))
                    {
                        using var memoryStream = new MemoryStream();
                        await thePicture.CopyToAsync(memoryStream);
                        var pictureArray = memoryStream.ToArray();//Gives us the Byte[]

                        //Check if we are replacing or creating new
                        if (athlete.AthletePhoto != null)
                        {
                            //We already have pictures so just replace the Byte[]
                            athlete.AthletePhoto.Content = ResizeImage.shrinkImageWebp(pictureArray, 200, 240);

                            //Get the Thumbnail so we can update it.  Remember we didn't include it
                            athlete.AthleteThumbnail = _context.AthleteThumbnails.Where(p => p.AthleteID == athlete.ID).FirstOrDefault();
                            athlete.AthleteThumbnail.Content = ResizeImage.shrinkImageWebp(pictureArray, 80, 96);
                        }
                        else //No pictures saved so start new
                        {
                            athlete.AthletePhoto = new AthletePhoto
                            {
                                Content = ResizeImage.shrinkImageWebp(pictureArray, 200, 240),
                                MimeType = "image/webp"
                            };
                            athlete.AthleteThumbnail = new AthleteThumbnail
                            {
                                Content = ResizeImage.shrinkImageWebp(pictureArray, 80, 96),
                                MimeType = "image/webp"
                            };
                        }
                    }
                }
            }
        }

        private async Task AddDocumentsAsync(Athlete athlete, List<IFormFile> theFiles)
        {
            foreach (var f in theFiles)
            {
                if (f != null)
                {
                    string mimeType = f.ContentType;
                    string fileName = Path.GetFileName(f.FileName);
                    long fileLength = f.Length;
                    //Note: you could filter for mime types if you only want to allow
                    //certain types of files.  I am allowing everything.
                    if (!(fileName == "" || fileLength == 0))//Looks like we have a file!!!
                    {
                        AthleteDocument d = new AthleteDocument();
                        using (var memoryStream = new MemoryStream())
                        {
                            await f.CopyToAsync(memoryStream);
                            d.FileContent.Content = memoryStream.ToArray();
                        }
                        d.FileContent.MimeType = mimeType;
                        d.FileName = fileName;
                        athlete.AthleteDocuments.Add(d);
                    };
                }
            }
        }

        public async Task<FileContentResult> Download(int id)
        {
            var theFile = await _context.UploadedFiles
                .Include(d => d.FileContent)
                .Where(f => f.ID == id)
                .FirstOrDefaultAsync();
            return File(theFile.FileContent.Content, theFile.FileContent.MimeType, theFile.FileName);
        }

        private SelectList CoachSelectList(int? selectedId)
        {
            return new SelectList(_context.Coaches
                .OrderBy(d => d.LastName)
                .ThenBy(d => d.FirstName), "ID", "FormalName", selectedId);
        }
        private SelectList ContingentSelectList(int? selectedId)
        {
            return new SelectList(_context.Contingents
                .OrderBy(d => d.Name), "ID", "Name", selectedId);
        }
        private SelectList HometownSelectList(int? ContingentID, int? selectedId)
        {
            var query = from c in _context.Hometowns.Include(c => c.Contingent)
                        where c.ContingentID == ContingentID.GetValueOrDefault()
                        select c;
            return new SelectList(query.OrderBy(p => p.Name), "ID", "HometownContingent", selectedId);
        }
        private SelectList GenderSelectList(int? selectedId)
        {
            return new SelectList(_context.Genders
                .OrderBy(d => d.Name), "ID", "Name", selectedId);
        }
        private SelectList SportSelectList(int? selectedId)
        {
            return new SelectList(_context.Sports
                .OrderBy(d => d.Name), "ID", "Name", selectedId);
        }
        private void PopulateDropDownLists(Athlete athlete = null)
        {
            ViewData["CoachID"] = CoachSelectList(athlete?.CoachID);
            ViewData["ContingentID"] = ContingentSelectList(athlete?.ContingentID);
            ViewData["GenderID"] = GenderSelectList(athlete?.GenderID);
            ViewData["SportID"] = SportSelectList(athlete?.SportID);
            ViewData["HometownID"] = HometownSelectList(athlete?.ContingentID, athlete?.HometownID);
        }

        [HttpGet]
        public JsonResult GetHometowns(int? ID)
        {
            return Json(HometownSelectList(ID, null));
        }

        private string ControllerName()
        {
            return this.ControllerContext.RouteData.Values["controller"].ToString();
        }
        private void ViewDataReturnURL()
        {
            ViewData["returnURL"] = MaintainURL.ReturnURL(HttpContext, ControllerName());
        }

        public IActionResult PlayerSummary()
        {
            var sumQ = _context.Placements.Include(a => a.Athlete)
                .GroupBy(a => new { a.AthleteID, a.Athlete.LastName, a.Athlete.FirstName, a.Athlete.MiddleName })
                .Select(grp => new PlacementReport
                {
                    ID = grp.Key.AthleteID,
                    FirstName = grp.Key.FirstName,
                    MiddleName = grp.Key.MiddleName,
                    LastName = grp.Key.LastName
                });

            return View(sumQ.AsNoTracking().ToList());
        }

        private bool AthleteExists(int id)
        {
            return _context.Athletes.Any(e => e.ID == id);
        }
    }
}
