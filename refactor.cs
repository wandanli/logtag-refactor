using System;

namespace LogTagOnline.API.Controllers
{
    [RoutePrefix("api/administration")]
    [System.Web.Mvc.OutputCacheAttribute(VaryByParam = "*", Duration = 0, NoStore = true)]
    public class AdministrationController : LTOBaseController
    {
        [Route("deleteuser/{userId}")]
        [SwaggerOperation("deleteuser/{userId}")]
        [SwaggerResponse(HttpStatusCode.OK)]
        [HttpGet]
        [LogTagExceptionFilter]
        public HttpResponseMessage DeleteUser(int userId, int teamId)
        {
            var loginUser = UserTeamWithTypeCheck(teamId);
            if (loginUser.ID != userId && loginUser.UserTeams.First().AccountTypeID == 3)
                throw new AuthenticationException(LogTagConstants.UserNotAuthorized);

            var userIsPrimaryCoordinator = DeleteUser1(userId, teamId);
            if (userIsPrimaryCoordinator == true) { return Request.CreateResponse(HttpStatusCode.Accepted, userIsPrimaryCoordinator); }

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        public bool DeleteUser1(int userId, int teamId)
        {
            var userIsPrimaryCoordinator = false;

            var userAreaEntity = (from userArea in DataAccess.UserAreas1
                                  join areaItem in DataAccess.Areas on userArea.AreaID equals areaItem.ID
                                  join teamItem in DataAccess.Teams on areaItem.TeamID equals teamItem.ID
                                  where areaItem.TeamID == teamId && userArea.UserAccountID == userId
                                  select userArea).FirstOrDefault();
            var areaId = userAreaEntity != null ? (int?)userAreaEntity.AreaID : null;
            var userAlerts = (from userAlert in DataAccess.LocationUserAlerts
                              join location in DataAccess.Locations on userAlert.LocationID equals location.ID
                              where location.TeamID == teamId && userAlert.UserAccountID == userId
                              select new
                              {
                                  UserAlert = userAlert,
                                  Location = location
                              }).ToList();
            var teamOwner = DataAccess.UserTeams.FirstOrDefault(x => x.TeamID == teamId && x.AccountTypeID == (int)UserAccountType.Owner);
            var allToRemove = new List<LocationUserAlert>();
            var allToAdd = new List<LocationUserAlert>();

            var user = DataAccess.UserAccounts.First(x => x.ID == userId);
            if (user.DistributorID != null)
            {
                var userteams = DataAccess.UserTeams.Where(x => x.UserAccountID == user.ID && x.Team.UserAccount.DistributorID == null && x.AccountTypeID == 4);
                DataAccess.UserTeams.RemoveRange(userteams);
            }
            foreach (var alert in userAlerts)
            {
                if (teamOwner != null)
                {
                    var locationAlert = DataAccess.LocationUserAlerts.Where(x => x.LocationID == alert.Location.ID).OrderBy(x => x.NotificationRole).ToList();
                    allToRemove.AddRange(locationAlert);
                    var newLocationAlert = new List<LocationUserAlert>();
                    foreach (var oldAlert in locationAlert)
                    {
                        if (oldAlert.NotificationRole == (int)NotificationRoleEnum.Primary)
                        {
                            userIsPrimaryCoordinator = true;
                            return userIsPrimaryCoordinator;
                        }
                        var newAlert = new LocationUserAlert()
                        {
                            EmailAlarm = oldAlert.EmailAlarm,
                            EmailBattery = oldAlert.EmailBattery,
                            EmailLostCon = oldAlert.EmailLostCon,
                            LocationID = oldAlert.LocationID,
                            NotificationRole = oldAlert.NotificationRole,
                            TextAlarm = oldAlert.TextAlarm,
                            TextBattery = oldAlert.TextBattery,
                            TextLostCon = oldAlert.TextLostCon,
                            UserAccountID = oldAlert.UserAccountID
                        };
                        newLocationAlert.Add(newAlert);
                    }
                    if (newLocationAlert.Count == 1)
                    {
                        newLocationAlert[0].UserAccountID = teamOwner.UserAccountID;
                    }
                    else if (newLocationAlert.Count == 2)
                    {
                        if (newLocationAlert[0].UserAccountID == userId)
                        {
                            newLocationAlert[0].UserAccountID = newLocationAlert[1].UserAccountID;

                        }
                        newLocationAlert.RemoveAt(1);
                    }
                    else if (newLocationAlert.Count == 3)
                    {
                        if (newLocationAlert[0].UserAccountID == userId)
                        {
                            newLocationAlert[0].UserAccountID = newLocationAlert[1].UserAccountID;
                            newLocationAlert[1].UserAccountID = newLocationAlert[2].UserAccountID;

                        }
                        else if (newLocationAlert[1].UserAccountID == userId)
                        {
                            newLocationAlert[1].UserAccountID = newLocationAlert[2].UserAccountID;
                        }
                        newLocationAlert.RemoveAt(2);
                    }
                    allToAdd.AddRange(newLocationAlert);
                }
            }

            var allToRemoveShipmentUserAlert = new List<ShipmentUserAlert>();
            var allToAddShipmentUserAlert = new List<ShipmentUserAlert>();
            var shipmentUserAlerts = (from userAlert in DataAccess.ShipmentUserAlerts
                              join ship in DataAccess.Shipments on userAlert.ShipmentID equals ship.ID
                              where ship.TeamID == teamId && userAlert.UserAccountID == userId
                              && (ship.ShipmentStateID == 1 || ship.ShipmentStateID == 2) // shipment is pending or in transit
                              select new
                              {
                                  UserAlert = userAlert,
                                  Shipment = ship
                              }).ToList();

            foreach (var alert in shipmentUserAlerts)
            {
                if (teamOwner != null)
                {
                    var shipmentAlert = DataAccess.ShipmentUserAlerts.Where(x => x.ShipmentID == alert.Shipment.ID).OrderBy(x => x.NotificationRole).ToList();
                    allToRemoveShipmentUserAlert.AddRange(shipmentAlert);
                    var newShipmentAlert = new List<ShipmentUserAlert>();
                    foreach (var oldAlert in shipmentAlert)
                    {
                        if (oldAlert.NotificationRole == (int)NotificationRoleEnum.Primary)
                        {
                            userIsPrimaryCoordinator = true;
                            return userIsPrimaryCoordinator;
                        }

                        var newAlert = new ShipmentUserAlert()
                        {
                            EmailAlarm = oldAlert.EmailAlarm,
                            TextAlarm = oldAlert.TextAlarm,
                            EmailStarted = oldAlert.EmailStarted,
                            TextStarted = oldAlert.TextStarted,
                            EmailFinish = oldAlert.EmailFinish,
                            TextFinish = oldAlert.TextFinish,
                            ShipmentID = oldAlert.ShipmentID,
                            NotificationRole = oldAlert.NotificationRole,
                            UserAccountID = oldAlert.UserAccountID
                        };
                        newShipmentAlert.Add(newAlert);
                    }
                    if (newShipmentAlert.Count == 1)
                    {
                        newShipmentAlert[0].UserAccountID = teamOwner.UserAccountID;
                    }
                    else if (newShipmentAlert.Count == 2)
                    {
                        if (newShipmentAlert[0].UserAccountID == userId)
                        {
                            newShipmentAlert[0].UserAccountID = newShipmentAlert[1].UserAccountID;

                        }
                        newShipmentAlert.RemoveAt(1);
                    }
                    else if (newShipmentAlert.Count == 3)
                    {
                        if (newShipmentAlert[0].UserAccountID == userId)
                        {
                            newShipmentAlert[0].UserAccountID = newShipmentAlert[1].UserAccountID;
                            newShipmentAlert[1].UserAccountID = newShipmentAlert[2].UserAccountID;

                        }
                        else if (newShipmentAlert[1].UserAccountID == userId)
                        {
                            newShipmentAlert[1].UserAccountID = newShipmentAlert[2].UserAccountID;
                        }
                        newShipmentAlert.RemoveAt(2);
                    }
                    allToAddShipmentUserAlert.AddRange(newShipmentAlert);
                }
            }
            DataAccess.LocationUserAlerts.RemoveRange(allToRemove);
            DataAccess.ShipmentUserAlerts.RemoveRange(allToRemoveShipmentUserAlert);
            DataAccess.SaveChanges();
            DataAccess.LocationUserAlerts.AddRange(allToAdd);
            DataAccess.ShipmentUserAlerts.AddRange(allToAddShipmentUserAlert);
            DataAccess.SaveChanges();

            DataAccess.DeleteTeamUser(userId, teamId, areaId);

            return userIsPrimaryCoordinator;
        }
    }
}
