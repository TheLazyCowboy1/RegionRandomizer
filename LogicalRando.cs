using RWCustom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RegionRandomizer;

internal class LogicalRando
{
    public static List<Connectible> RandomlyConnectConnectibles(List<Connectible> connectibles, float randomness = 0.5f, int triesRemaining = 3, int fruitlessTestLimit = 20)
    {
        try
        {
            //make list of connectibles that need to be connected
            List<Connectible> notConnected = new();
            List<Connectible> connected = new();

            //always add the first Connectible in the input list to connected
            //if I want it to be random, I should shuffle before calling the function
            //connected.Add(connectibles[0].FreshClone());

            //for (int i = 1; i < connectibles.Count; i++)
            //notConnected.Add(connectibles[i].FreshClone());
            bool firstNonFixedAdded = false;
            foreach (Connectible connectible in connectibles)
            {
                Connectible c = connectible.FreshClone();
                if (c.fixedPosition)
                    connected.Add(c);
                else if (!firstNonFixedAdded)
                {
                    c.position = new Vector2(0, 0);
                    c.accessible = true;
                    connected.Add(c);
                    firstNonFixedAdded = true;
                }
                else
                    notConnected.Add(c);
            }
            if (!firstNonFixedAdded) //just to ensure it doesn't break if somehow all are already fixed
                connected[0].accessible = true;

            //add potential connections between connected
            foreach (Connectible c in connected)
                c.AddPotentialConnections(c, connected);

            //connect all of notConnected to connected
            string connectionPattern = "Connection Pattern: ";
            long startTime = System.DateTime.Now.Ticks;
            int testsSaved = 0, totalTests = 0;
            while (notConnected.Count > 0)
            {
                //cycle through each of notConnected, get its best possible score and where
                //then pick the best scored option. man, this is about to be a lot of recursion...
                float lowestScore = float.PositiveInfinity;
                int lowestNotConnectedIdx = -1;
                string lowestNotConnectedConn = "";
                int lowestConnectedIdx = -1;
                string lowestConnectedConn = "";

                int freeConnectionCount = 0;
                foreach (Connectible c in connected)
                {
                    foreach (string conn in c.connections.Values)
                    {
                        if (conn == "")
                            freeConnectionCount++;
                    }
                }

                //implementing some optimization...
                List<int> notConnectedTestOrder = new();
                for (int i = 0; i < notConnected.Count; i++)
                    notConnectedTestOrder.Add(i);

                for (int connectedIdx = 0; connectedIdx < connected.Count; connectedIdx++) //cycle through connected
                {
                    Connectible cc = connected[connectedIdx];

                    foreach (string conn in cc.connections.Keys) //cycle through each free connection in connected
                    {
                        if (cc.connections[conn] != "") //only process free connections
                            continue;

                        float lowScore = float.PositiveInfinity;
                        int lowNCIdx = -1;
                        string lowNCConn = "";
                        string lowCConn = "";
                        int connsSinceLowerScore = Int32.MinValue;

                        //notConnected.Shuffle(); //shuffles notConnected to avoid repeatedly testing the same bad options
                        //notConnected = ShuffleList(notConnected);
                        notConnectedTestOrder = ShuffleList(notConnectedTestOrder);

                        //for (int notIdx = 0; notIdx < notConnected.Count; notIdx++) //cycle through notConnected
                        foreach (int notIdx in notConnectedTestOrder)
                        {
                            Connectible nc = notConnected[notIdx];
                            bool deadEnd = nc.connections.Count < 2;

                            if (notConnected.Count > 1 && deadEnd && freeConnectionCount < 2)
                                continue; //prevents connecting a dead-end when there is only one free connection left

                            //if dead end, only allow accessible connected
                            //add very severe penalty (instead of calling continue)
                            float ccPenalty = 0;
                            if (deadEnd && !cc.accessible)
                                //continue;
                                ccPenalty = 1000000f;

                            var connData = cc.ResolveBestConnection(nc, conn, connected);
                            //also factor in what connections I would be stealing...
                            float score = connData.Value;// + Connectible.ScoreDiffWhenConnectionTaken(cc.name + ";" + conn, ";", connected);

                            score = score - score * UnityEngine.Random.value * randomness; //randomize score
                            score += ccPenalty; //discourage dead-ends

                            if (score < lowScore)
                            {
                                lowNCIdx = notIdx;
                                lowNCConn = connData.Key;
                                lowCConn = conn;
                                lowScore = score;
                                connsSinceLowerScore = 0;
                            }
                            else if (connsSinceLowerScore >= fruitlessTestLimit)
                            {
                                testsSaved += notConnected.Count - notIdx - 1;
                                break;
                            }
                            else
                                connsSinceLowerScore++;
                        }
                        totalTests += notConnected.Count;

                        if (lowScore < lowestScore && lowNCIdx >= 0)
                        {
                            lowestNotConnectedIdx = lowNCIdx;
                            lowestNotConnectedConn = lowNCConn;
                            lowestConnectedIdx = connectedIdx;
                            lowestConnectedConn = lowCConn;
                            lowestScore = lowScore;
                        }
                    }
                }


                if (lowestNotConnectedIdx < 0 || lowestConnectedIdx < 0)
                {
                    RegionRandomizer.LogSomething("Could not find any notConnected to connect to connected!! notConnected.Count: " + notConnected.Count + ", free connections: " + freeConnectionCount);
                    break;
                }
                else if (lowestConnectedConn == "" || lowestNotConnectedConn == "")
                {
                    RegionRandomizer.LogSomething("Somehow didn't get correct connection data??? notConnected.Count: " + notConnected.Count + ", free connections: " + freeConnectionCount);
                }

                //connect the best option!
                Connectible best = notConnected[lowestNotConnectedIdx];

                connected.Add(best); //added here so that all the proper potential connections are ensured to be removed
                try
                {
                    best.ConnectToOtherConnectible(connected[lowestConnectedIdx], lowestNotConnectedConn, lowestConnectedConn, connected);
                }
                catch (Exception ex)
                {
                    RegionRandomizer.LogSomething(ex);
                    RegionRandomizer.LogSomething("Connection error?? " + best.name + ": " + lowestNotConnectedConn);
                }

                //connected.Add(best);
                notConnected.RemoveAt(lowestNotConnectedIdx);

                connectionPattern += best.name + " to " + connected[lowestConnectedIdx].name + " (" + lowestScore + "); ";
            }
            RegionRandomizer.LogSomething(connectionPattern);
            RegionRandomizer.LogSomething("Time 1: " + (System.DateTime.Now.Ticks - startTime));
            RegionRandomizer.LogSomething("Optimization Amount: " + ((float)testsSaved / (float)totalTests));

            foreach (Connectible c in notConnected)
                c.Clear();
            notConnected.Clear(); //we have no use of this anymore


            //now that everything is in connected, start making connections!
            connectionPattern = "Connection Pattern 2: ";
            startTime = System.DateTime.Now.Ticks;

            //make a nameToIdx dictionary for simplicity's sake
            Dictionary<string, int> nameToIdx = new();
            for (int i = 0; i < connected.Count; i++)
                nameToIdx.Add(connected[i].name, i);

            //FIRST, try to connect every inaccessible connectible to an accessible one
            bool inaccessibleLeft = true;
            while (inaccessibleLeft)
            {
                //determine if there are any inaccessible left AND list all accessible connectibles (for potential connections)
                List<string> accessible = new();
                if (inaccessibleLeft) //check if there really are inaccessible connectibles left
                {
                    foreach (Connectible c in connected)
                    {
                        if (c.accessible || c.allConnectionsMade)
                            accessible.Add(c.name);
                    }
                    inaccessibleLeft = accessible.Count < connected.Count;
                }
                if (!inaccessibleLeft)
                    break;

                //find best connection
                float lowestScore = float.PositiveInfinity;
                string lowestName = "";
                string lowestConn = "";
                string lowestConnection = "";

                //loop through each inaccessible connectible
                foreach (Connectible c in connected)
                {
                    if (c.accessible || c.allConnectionsMade)
                        continue;

                    //loop through each conn in c
                    foreach (string conn in c.connections.Keys)
                    {
                        if (c.connections[conn] != "")
                            continue;

                        //loop through each potential connection... to a point
                        float cLowestScore = float.PositiveInfinity;
                        string cLowestConnection = "";
                        int testsSinceLowerScore = Int32.MinValue;

                        if (c.potentialConnections[conn].Count < 1)
                        {
                            RegionRandomizer.LogSomething("No potential connections for " + c.name + ": " + conn);
                            c.AddPotentialConnectionsLatter(connected);
                            if (c.potentialConnections[conn].Count < 1)
                                RegionRandomizer.LogSomething("STILL no potential connections for " + c.name + ": " + conn);
                        }

                        for (int i = 0; i < c.potentialConnections[conn].Count; i++)
                        {
                            if (!accessible.Contains(c.potentialConnections[conn][i].Split(';')[0]))
                                continue; //only consider accessible potential connections
                            //note: this would be a good place to add logic for which connections are ACTUALLY accessible

                            float score = c.potentialConnectionScores[conn][i];
                            //score += score - c.estimatedConnectionScores[conn];
                            score += Connectible.ScoreDiffWhenConnectionTaken(c.name + ";" + conn, c.potentialConnections[conn][i], score, connected);
                            score = score - score * UnityEngine.Random.value * randomness; //randomize score

                            if (score < cLowestScore)
                            {
                                cLowestScore = score;
                                cLowestConnection = c.potentialConnections[conn][i];
                                testsSinceLowerScore = 0;
                            }
                            else if (testsSinceLowerScore >= 3) //cap at 3 unsuccessful tests
                                break;
                            else
                                testsSinceLowerScore++;
                        }

                        if (cLowestScore < lowestScore)
                        {
                            lowestScore = cLowestScore;
                            lowestName = c.name;
                            lowestConn = conn;
                            lowestConnection = cLowestConnection;
                        }
                    }
                }

                //make the connection

                if (lowestConn == "" || !nameToIdx.ContainsKey(lowestName))
                {
                    RegionRandomizer.LogSomething("Couldn't find any other free ACCESSIBLE connection!! " + lowestName + ", " + lowestConn);
                    break;
                }

                string accName = lowestConnection.Split(';')[0];
                string accConn = lowestConnection.Split(';')[1];

                int accIdx = nameToIdx[accName];
                int inacIdx = nameToIdx[lowestName];

                connected[inacIdx].MakeConnection(connected[accIdx], lowestConn, accConn, connected);

                connectionPattern += "(i)" + connected[inacIdx].name + ";" + lowestConn + "--" + connected[accIdx].name + ";" + accConn + " (" + lowestScore + "); ";

            }


            bool thingsStillUnconnected = true;
            while (thingsStillUnconnected)
            {
                //pick the connectible with the highest number of free connections
                int connIdx = -1;
                int highestFreeCount = 0;
                List<string> freeConns = new();
                for (int i = 0; i < connected.Count; i++)
                {
                    List<string> free = new();
                    foreach (var conn in connected[i].connections)
                    {
                        if (conn.Value == "")
                        {
                            if (connected[i].potentialConnectionScores[conn.Key].Count > 0)
                                free.Add(conn.Key);
                            else
                            {
                                //RegionRandomizer.LogSomething(connectionPattern + " ... FAILED!");
                                RegionRandomizer.LogSomething("No potential connections for " + connected[i].name + ": " + conn.Key);
                                connected[i].AddPotentialConnectionsLatter(connected);
                                if (connected[i].potentialConnectionScores[conn.Key].Count > 0)
                                    free.Add(conn.Key);
                                else
                                    RegionRandomizer.LogSomething("STILL no potential connections for " + connected[i].name + ": " + conn.Key);
                            }
                        }
                    }
                    if (free.Count > highestFreeCount)
                    {
                        highestFreeCount = free.Count;
                        freeConns.Clear();
                        freeConns = free;
                        connIdx = i;
                    }
                    else
                        free.Clear();
                }

                if (highestFreeCount < 1)
                {
                    thingsStillUnconnected = false;
                    break;
                }

                //pick a random one of freeConnections
                //int freeConnRandIdx = UnityEngine.Random.Range(0, freeConns.Count);
                //string connName = freeConns[freeConnRandIdx];

                //pick the free connection with the highest (worst) first potential connection score
                int highestPotentialScoreIdx = 0;
                float highestPotentialScore = float.NegativeInfinity;
                for (int i = 0; i < freeConns.Count; i++)
                {
                    if (connected[connIdx].potentialConnectionScores[freeConns[i]][0] > highestPotentialScore)
                    {
                        highestPotentialScore = connected[connIdx].potentialConnectionScores[freeConns[i]][0];
                        highestPotentialScoreIdx = i;
                    }
                }
                string connName = freeConns[highestPotentialScoreIdx];

                float lowestScore = float.PositiveInfinity;
                int lowestIdx = -1;
                string lowestConn = "";
                int testsSinceLowerScore = int.MinValue;

                for (int i = 0; i < connected[connIdx].potentialConnections[connName].Count; i++)
                {
                    float score = connected[connIdx].potentialConnectionScores[connName][i];
                    //score += score - connected[connIdx].estimatedConnectionScores[connName];
                    score += Connectible.ScoreDiffWhenConnectionTaken(connected[connIdx].name + ";" + connName, connected[connIdx].potentialConnections[connName][i], score, connected);
                    //increase score so it is positive
                    //score += Connectible.CANT_CONNECT_SCORE_CAP * Connectible.CANT_CONNECT_SCORE_CAP;

                    score = score - score * UnityEngine.Random.value * randomness; //randomize score

                    if (score < lowestScore)
                    {
                        lowestScore = score;
                        lowestConn = connected[connIdx].potentialConnections[connName][i].Split(';')[1];
                        string cname = connected[connIdx].potentialConnections[connName][i].Split(';')[0];
                        for (int j = 0; j < connected.Count; j++)
                        {
                            if (connected[j].name == cname)
                            {
                                lowestIdx = j;
                                break;
                            }
                        }
                        testsSinceLowerScore = 0;
                    }
                    else
                    {
                        testsSinceLowerScore++;
                        if (testsSinceLowerScore > 2) //caps it at 3 unsuccessful tests
                            break;
                    }
                }

                if (lowestIdx < 0)
                {
                    RegionRandomizer.LogSomething("Couldn't find any other free connection!! " + connected[connIdx].name + ", " + connName);
                    //continue;
                    break;
                }

                connected[connIdx].MakeConnection(connected[lowestIdx], connName, lowestConn, connected);

                connectionPattern += connected[connIdx].name + ";" + connName + "--" + connected[lowestIdx].name + ";" + lowestConn + " (" + lowestScore + "); ";
                //if (!freeConnections.Remove(new KeyValuePair<int, string>(lowestIdx, lowestConn)))
                //RegionRandomizer.LogSomething("Failed to remove connection " + lowestConn);

            }

            RegionRandomizer.LogSomething(connectionPattern);
            RegionRandomizer.LogSomething("Time 2: " + (System.DateTime.Now.Ticks - startTime));


            //check that everything was done properly
            if (triesRemaining > 0)
            {
                bool strikeOne = false;
                foreach (Connectible c in connected)
                {
                    if (c.connections.Values.Contains("") || c.connections.Values.Contains(c.name))
                    {
                        if (!strikeOne)
                        {
                            strikeOne = true;
                            continue;
                        }

                        RegionRandomizer.LogSomething("Missing two connections!! Rerandomizing connectibles.");

                        //connectibles.Shuffle(); //try starting with a different connectible; just in case
                        connectibles = ShuffleList(connectibles);
                        foreach (Connectible c2 in connected)
                            c2.Clear();
                        connected.Clear();

                        return RandomlyConnectConnectibles(connectibles, (randomness < 1f) ? randomness + 0.1f : randomness, --triesRemaining);
                    }
                }
            }

            //RegionRandomizer.LogSomething("Calls to ConnectionScore: " + Connectible.callsToConnectionScore);
            //RegionRandomizer.LogSomething("Calls to ScoreDiff: " + Connectible.callsToScoreDiff);

            return connected;
        }
        catch (Exception ex)
        {
            RegionRandomizer.LogSomething(ex);
            return new List<Connectible>();
        }

    }

    //private static void ExtraDebug(object obj)
    //{
    //RegionRandomizer.LogSomething(obj);
    //}

    //connectible class
    #region Connectible
    public class Connectible
    {
        //debug
        //public static int callsToConnectionScore = 0;
        //public static int callsToScoreDiff = 0;

        //ARBITRARY NUMBERS!!!
        //private const float EST_DIST_SQR = 1000f * 1000f; //the estimated distance between two rooms, squared
        public const bool PROHIBIT_DOUBLE_CONNECTIONS = true; //MUST BE TRUE. prevents connection[a] and connection[b] from having the same value
        
        public const float DISTANCE_SCORE_MODIFIER = 10f;
        public const float ORIGINAL_DISTANCE_SCORE_MODIFIER = 5f; //this ought to be a config. Groups rooms together nicely
        public const float CONNECTION_DISTANCE_MODIFIER = 4f;
        public const float QUAD_CONNECTION_DISTANCE_MODIFIER = 384f; //the square of CONNECTION_DISTANCE score
        public const float PLACEMENT_ANGLE_MODIFIER = 1f;
        public const float ANGLE_SCORE_MODIFIER = 3f; //set to lower because it's not important in vanilla (e.g: LF and SB)
        public const float BONUS_ANGLE_PLACEMENT_MODIFIER = 5f; //applies only when snapping a connectible into place
        public const float SAME_CONN_COUNT_PENALTY = 320f; //discourages chains of two-connection-connectibles
        public const float SCORE_POTENTIAL_MODIFIER = 1.0f;
        public const float NEXT_POTENTIAL_SCORE_MODIFIER = 0.2f;
        public const int POTENTIAL_CONNECTIONS_CAP = 60; //caps the number of potential connections to improve randomization times
        public const float POTENTIAL_CONN_DIST_LIMIT = 16f;
        public static void SetConstants()
        {
        }

        public const float DIFF_GROUP_SCORE_PENALTY = 1048576f; //heavily encourages different groups not to... intermingle
        public const float SINGLE_CONNECTION_SCORE_PENALTY = 1048576f; //delays dead end connections like LC until later
        public const float CANT_CONNECT_SCORE_CAP = 1048576f; //prevents the score from becoming infinitely high

        public string name;
        //each Vector2 is relative to the center of the connectible
        public Dictionary<string, Vector2> connLocations;
        public Dictionary<string, Vector2> connLocationsNormalized = new();
        public Dictionary<string, string> connections;
        public Vector2 position;
        public Vector2 originalPosition;
        public bool fixedPosition;
        public string group;
        public float radius;

        //potential connections code
        public bool allConnectionsMade = false;
        public bool accessible = false;
        public Dictionary<string, List<float>> potentialConnectionScores = new();
        public Dictionary<string, float> estimatedConnectionScores = new();
        public Dictionary<string, List<string>> potentialConnections = new(); //connections stored in NAME;CONNKEY format

        public Connectible(string name, Dictionary<string, Vector2> connLocations) : this(name, connLocations, new Vector2(0, 0), false, "none")
        { }
        public Connectible(string name, Dictionary<string, Vector2> connLocations, string group = "none") : this(name, connLocations, new Vector2(0, 0), false, group)
        { }
        public Connectible(string name, Dictionary<string, Vector2> connLocations, Vector2 position, bool fixedPosition = false, string group = "none")
        {
            SetConstants();

            this.name = name;
            this.connLocations = connLocations;
            this.connections = new();
            this.position = position;
            this.originalPosition = new Vector2(position.x, position.y);
            this.fixedPosition = fixedPosition;
            this.group = group;

            float sqrRad = 0;
            foreach (var conn in connLocations)
            {
                this.connections.Add(conn.Key, "");
                this.connLocationsNormalized.Add(conn.Key, conn.Value.normalized);
                sqrRad = Mathf.Max(sqrRad, conn.Value.SqrMagnitude());

                potentialConnectionScores.Add(conn.Key, new());
                potentialConnections.Add(conn.Key, new());
                estimatedConnectionScores.Add(conn.Key, new());
            }
            this.radius = Mathf.Sqrt(sqrRad);
        }

        public override bool Equals(object obj)
        {
            return obj is Connectible && (obj as Connectible).name == this.name;
        }

        public Connectible FreshClone()
        {
            return new Connectible(name, connLocations, originalPosition, fixedPosition, group);
        }

        public void Clear()
        {
            connLocations.Clear();
            connLocationsNormalized.Clear();
            connections.Clear();

            foreach (List<float> l in potentialConnectionScores.Values)
                l.Clear();
            potentialConnectionScores.Clear();

            estimatedConnectionScores.Clear();

            foreach (List<string> l in potentialConnections.Values)
                l.Clear();
            potentialConnections.Clear();
        }


        public Vector2 WorldPosition(string conn)
        {
            return this.position + connLocations[conn];
        }

        public void MakeConnection(Connectible c, string thisConn, string thatConn)
        {
            this.connections[thisConn] = c.name;
            c.connections[thatConn] = this.name;

            this.allConnectionsMade = true;
            foreach (string v in this.connections.Values)
            {
                if (v == "")
                {
                    this.allConnectionsMade = false;
                    break;
                }
            }
            //clear potential connections info
            if (this.allConnectionsMade)
            {
                foreach (List<float> l in this.potentialConnectionScores.Values)
                    l.Clear();
                //this.potentialConnectionScores.Clear();
                foreach (List<string> l in this.potentialConnections.Values)
                    l.Clear();
                //this.potentialConnections.Clear();
            }
        }


        //also removes potential connections from other connectibles
        public void MakeConnection(Connectible c, string thisConn, string thatConn, List<Connectible> otherConnectibles)
        {
            this.MakeConnection(c, thisConn, thatConn);

            string connection1 = this.name + ";" + thisConn;
            string connection2 = c.name + ";" + thatConn;

            //remove all instances of the potential connections made (connection1 and connection2)
            foreach (Connectible other in otherConnectibles)
            {
                foreach (string key in other.potentialConnections.Keys)
                {
                    int idx = other.potentialConnections[key].IndexOf(connection1);
                    if (idx >= 0)
                    {
                        other.potentialConnections[key].RemoveAt(idx);
                        other.potentialConnectionScores[key].RemoveAt(idx);
                        other.EstimateConnectionScore(key);
                    }

                    idx = other.potentialConnections[key].IndexOf(connection2);
                    if (idx >= 0)
                    {
                        other.potentialConnections[key].RemoveAt(idx);
                        other.potentialConnectionScores[key].RemoveAt(idx);
                        other.EstimateConnectionScore(key);
                    }
                }
            }

            this.potentialConnections[thisConn].Clear();
            c.potentialConnections[thatConn].Clear();
            this.potentialConnectionScores[thisConn].Clear();
            c.potentialConnectionScores[thatConn].Clear();

            //remove all connections to c in this
            foreach (string conn in this.potentialConnections.Keys)
            {
                for (int i = this.potentialConnections[conn].Count - 1; i >= 0; i--)
                {
                    if (this.potentialConnections[conn][i].StartsWith(c.name))
                    {
                        this.potentialConnections[conn].RemoveAt(i);
                        this.potentialConnectionScores[conn].RemoveAt(i);
                        this.EstimateConnectionScore(conn);
                    }
                }
            }

            //remove all connections to this in c
            foreach (string conn in c.potentialConnections.Keys)
            {
                for (int i = c.potentialConnections[conn].Count - 1; i >= 0; i--)
                {
                    if (c.potentialConnections[conn][i].StartsWith(this.name))
                    {
                        c.potentialConnections[conn].RemoveAt(i);
                        c.potentialConnectionScores[conn].RemoveAt(i);
                        c.EstimateConnectionScore(conn);
                    }
                }
            }

            //accessibility check
            if (this.accessible && !c.accessible)
                c.MakeAccessible(otherConnectibles);
            else if (c.accessible && !this.accessible)
                this.MakeAccessible(otherConnectibles);

        }
        //makes this connectible accessible AND calls this function for all connected inaccessible connectibles
        public void MakeAccessible(List<Connectible> otherConnectibles)
        {
            this.accessible = true;

            //check any inaccessible connections
            foreach (Connectible c in otherConnectibles)
            {
                if (!c.accessible && this.connections.ContainsValue(c.name))
                    c.MakeAccessible(otherConnectibles);
            }
        }

        public void SnapToConnection(Connectible c, string thisConn, string thatConn)
        {
            this.position = c.WorldPosition(thatConn) - this.connLocations[thisConn];
        }

        //used for connecting one of notConnected to the rest of connected
        //also adds potential connections
        public void ConnectToOtherConnectible(Connectible c, string thisConn, string thatConn, List<Connectible> otherConnectibles)
        {
            this.SnapToConnection(c, thisConn, thatConn);
            this.MakeConnection(c, thisConn, thatConn, otherConnectibles);

            this.AddPotentialConnections(c, otherConnectibles);
        }

        public void AddPotentialConnections(Connectible c, List<Connectible> otherConnectibles)
        {
            //calculate and add potential connection scores
            foreach (Connectible other in otherConnectibles)
            {
                if (other.name == this.name || other.name == c.name) //don't add itself as a potential connections
                    continue;

                foreach (string conn in this.connections.Keys)
                {
                    if (this.connections[conn] != "")
                        continue;
                    foreach (string conn2 in other.connections.Keys)
                    {
                        if (other.connections[conn2] != "")
                            continue;
                        float score = this.ConnectionScore(other, conn, conn2);
                        this.AddPotentialConnection(conn, other.name + ";" + conn2, score);
                        other.AddPotentialConnection(conn2, this.name + ";" + conn, score);
                    }
                }
            }
        }

        //adds extra logic; designed to be used midway through the randomization process
        public void AddPotentialConnectionsLatter(List<Connectible> otherConnectibles)
        {
            //calculate and add potential connection scores
            foreach (Connectible other in otherConnectibles)
            {
                if (other.name == this.name || other.connections.ContainsValue(this.name)) //don't add itself as a potential connections
                    continue;

                foreach (string conn in this.connections.Keys)
                {
                    if (this.connections[conn] != "")
                        continue;
                    foreach (string conn2 in other.connections.Keys)
                    {
                        if (other.connections[conn2] != "")
                            continue;
                        float score = this.ConnectionScore(other, conn, conn2);
                        this.AddPotentialConnection(conn, other.name + ";" + conn2, score);
                        other.AddPotentialConnection(conn2, this.name + ";" + conn, score);
                    }
                }
            }
        }


        //potential connections code
        public void AddPotentialConnection(string thisConn, string thatConnection, float score)
        {
            //don't add the connection IF we have reached the cap AND the new connection is worse
            if (this.potentialConnectionScores[thisConn].Count >= POTENTIAL_CONNECTIONS_CAP
                && this.potentialConnectionScores[thisConn][POTENTIAL_CONNECTIONS_CAP - 1] <= score)
                return;

            //don't add the connection if we already have it
            if (this.potentialConnections[thisConn].Contains(thatConnection))
                return;

            int idx = -1; //idx of first higher score
            for (int i = 0; i < potentialConnectionScores[thisConn].Count; i++)
            {
                if (potentialConnectionScores[thisConn][i] > score)
                {
                    idx = i;
                    break;
                }
            }

            if (idx < 0)
            {
                this.potentialConnectionScores[thisConn].Add(score);
                this.potentialConnections[thisConn].Add(thatConnection);
            }
            else
            {
                this.potentialConnectionScores[thisConn].Insert(idx, score);
                this.potentialConnections[thisConn].Insert(idx, thatConnection);
            }

            //remove the worst potential connection IF we have reached the cap
            if (this.potentialConnections[thisConn].Count > POTENTIAL_CONNECTIONS_CAP)
            {
                this.potentialConnections[thisConn].Pop();
                this.potentialConnectionScores[thisConn].Pop();
            }

            this.EstimateConnectionScore(thisConn);

        }
        private void EstimateConnectionScore(string thisConn)
        {
            float total = 0;
            float tempMod = 1f;
            foreach (float score in this.potentialConnectionScores[thisConn])
            {
                total += score * tempMod;
                tempMod *= NEXT_POTENTIAL_SCORE_MODIFIER;
            }
            total += CANT_CONNECT_SCORE_CAP * tempMod;

            this.estimatedConnectionScores[thisConn] = total;
        }

        //thisConnection and thatConnection are the two connections being connected, in form NAME;CONN
        public float OLD_ScoreDiffWhenConnectionTaken(string thisConnection, string thatConnection, List<Connectible> otherConnectibles)
        {
            float total = 0;

            //find diff between each potential connection thisConnection and the next potential connection
            foreach (Connectible c in otherConnectibles)
            {
                foreach (string thatConn in c.potentialConnections.Keys)
                {
                    string connectionString = c.name + ";" + thatConn;
                    if (connectionString == thisConnection || connectionString == thatConnection) //don't check itself
                        continue;
                    if (c.connections[thatConn] != "") //only check non-connected connections
                        continue;

                    int idx = c.potentialConnections[thatConn].IndexOf(thisConnection);
                    if (idx < 0)
                        continue;
                    if (idx < c.potentialConnectionScores[thatConn].Count - 1)
                    {
                        total += QuickPow(0.5f, idx) * (c.potentialConnectionScores[thatConn][idx + 1] - c.potentialConnectionScores[thatConn][idx]);
                    }
                    else //if there is no secondary connection to replace it
                    {
                        //replaced CANT_CONNECT_SCORE_CAP with float.PositiveInfinity to try to reduce impossible situations
                        //infinity causes problems... instead I'm using CANT_CONNECT_SCORE_CAP squared
                        //total += QuickPow(0.5f, idx) * (CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP - c.potentialConnectionScores[thatConn][idx]);
                        total += QuickPow(0.5f, idx) * CANT_CONNECT_SCORE_CAP;
                    }
                }
            }

            //(copied from above) find diff between each potential connection thatConnection and the next potential connection
            foreach (Connectible c in otherConnectibles)
            {
                foreach (string thatConn in c.potentialConnections.Keys)
                {
                    string connectionString = c.name + ";" + thatConn;
                    if (connectionString == thisConnection || connectionString == thatConnection) //don't check itself
                        continue;
                    if (c.connections[thatConn] != "") //only check non-connected connections
                        continue;

                    int idx = c.potentialConnections[thatConn].IndexOf(thatConnection);
                    if (idx < 0)
                        continue;
                    if (idx < c.potentialConnectionScores[thatConn].Count - 1)
                    {
                        total += QuickPow(0.5f, idx) * (c.potentialConnectionScores[thatConn][idx + 1] - c.potentialConnectionScores[thatConn][idx]);
                    }
                    else //if there is no secondary connection to replace it
                    {
                        //total += QuickPow(0.5f, idx) * (CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP - c.potentialConnectionScores[thatConn][idx]);
                        total += QuickPow(0.5f, idx) * CANT_CONNECT_SCORE_CAP;
                    }
                }
            }


            string thisConnName = thisConnection.Split(';')[0];
            string thatConnName = thatConnection.Split(';')[0];


            //check whether this connectible will allow other connectibles to connect everything
            bool thisConnectibleHasFreeConns = false;
            foreach (Connectible c in otherConnectibles)
            {
                if (c.name != thisConnName)
                    continue;
                List<string> l = c.connections.Values.ToList();
                int idx = l.IndexOf("");
                thisConnectibleHasFreeConns = idx >= 0 && l.IndexOf("", idx + 1) > idx;
                break;
            }
            bool thatConnectibleHasFreeConns = false;
            foreach (Connectible c in otherConnectibles)
            {
                if (c.name != thatConnName)
                    continue;
                List<string> l = c.connections.Values.ToList();
                int idx = l.IndexOf("");
                thatConnectibleHasFreeConns = idx >= 0 && l.IndexOf("", idx + 1) > idx;
                break;
            }

            foreach (Connectible c in otherConnectibles)
            {
                if (c.name == thisConnName || c.name == thatConnName)
                    continue;
                int freeConnCount = 0;
                foreach (string s in c.connections.Values)
                {
                    if (s == "")
                        freeConnCount++;
                }
                List<string> potentialRegions = new List<string>();
                foreach (Connectible c2 in otherConnectibles)
                {
                    if (c2.name != c.name && c2.connections.ContainsValue("") && !c2.connections.ContainsValue(c.name))
                        potentialRegions.Add(c2.name);
                }
                if (!thisConnectibleHasFreeConns)
                    potentialRegions.Remove(thisConnName);
                if (!thatConnectibleHasFreeConns)
                    potentialRegions.Remove(thatConnName);

                if (potentialRegions.Count < freeConnCount)
                    total += CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP; //hefty penalty

                potentialRegions.Clear();
            }

            //consider the limitation that each connectible can only connect to another connectible (c) once
            //does not search EACH connection denied... because I'm too lazy to implement that loop, and it seems over-kill
            foreach (Connectible c in otherConnectibles)
            {
                if (c.name != thisConnName) //c should = this
                    continue;

                foreach (string thisConn in c.potentialConnections.Keys)
                {
                    if (c.connections[thisConn] != "") //only check non-connected connections
                        continue;

                    int idx = c.potentialConnections[thisConn].Count; //idx of first non-c connection
                    for (int i = 0; i < idx; i++)
                    {
                        if (!c.potentialConnections[thisConn][i].StartsWith(thatConnName))
                            idx = i;
                    }
                    if (idx == 0)
                        continue;
                    if (idx < c.potentialConnections[thisConn].Count)
                    {
                        total += c.potentialConnectionScores[thisConn][idx] - c.potentialConnectionScores[thisConn][0];
                    }
                    else //if there is no secondary connection to replace it
                    {
                        //total += CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP - c.potentialConnectionScores[thisConn][0];
                        total += CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP;
                    }
                }
                break;
            }

            //exact same but with thatConnection instead
            foreach (Connectible c in otherConnectibles)
            {
                if (c.name != thatConnName) //c should = this
                    continue;

                foreach (string thisConn in c.potentialConnections.Keys)
                {
                    if (c.connections[thisConn] != "") //only check non-connected connections
                        continue;

                    int idx = c.potentialConnections[thisConn].Count; //idx of first non-c connection
                    for (int i = 0; i < idx; i++)
                    {
                        if (!c.potentialConnections[thisConn][i].StartsWith(thisConnName))
                            idx = i;
                    }
                    if (idx == 0)
                        continue;
                    if (idx < c.potentialConnections[thisConn].Count)
                    {
                        total += c.potentialConnectionScores[thisConn][idx] - c.potentialConnectionScores[thisConn][0];
                    }
                    else //if there is no secondary connection to replace it
                    {
                        //total += CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP - c.potentialConnectionScores[thisConn][0];
                        total += CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP;
                    }
                }
                break;
            }

            return SCORE_POTENTIAL_MODIFIER * total;
        }

        public static float ScoreDiffWhenConnectionTaken(string thisConnection, string thatConnection, float connScore, List<Connectible> otherConnectibles)
        {
            //ExtraDebug("Testing score diffs for " + thisConnection + " and " + thatConnection);
            //plan: go through each connectible in otherConnectibles and fully calculate the estimated score before vs. after
            float total = 0;
            string thisConnName = thisConnection.Split(';')[0];
            string thatConnName = thatConnection.Split(';')[0];

            foreach (Connectible c in otherConnectibles)
            {
                foreach (string conn in c.potentialConnections.Keys)
                {
                    float origTotal = total;
                    //callsToScoreDiff++;
                    if (c.connections[conn] != "") //skip already connected
                        continue;

                    string connectionString = c.name + ";" + conn;
                    if (connectionString == thisConnection || connectionString == thatConnection)
                    {
                        //calculate differently:
                        //compare the score of what's taken to the estimated score
                        total += connScore - c.estimatedConnectionScores[conn];
                        //ExtraDebug("Scorediff for " + c.name + "(" + conn + ") == " + (total - origTotal));
                        continue;
                    }

                    //get current score
                    float currScore = c.estimatedConnectionScores[conn];

                    //get revised score
                    float newScore = 0;
                    float tempMod = 1f;
                    for (int i = 0; i < c.potentialConnections[conn].Count; i++)
                    {
                        string thatConn = c.potentialConnections[conn][i];
                        //skip potential connections that match thisConnection or thatConnection
                        //(since once thisConnection and thatConnection are made, they cannot be used by others)
                        if (thatConn == thisConnection || thatConn == thatConnection)
                        {
                            //if (scoreMod < 0) break; //designed to heavily encourage earlier potential scores
                            continue;
                        }
                        //skip potential connections to regions that will be taken up
                        if (c.name == thisConnName)
                        {
                            if (thatConn.StartsWith(thatConnName))
                            {
                                continue;
                            }
                        }
                        else if (c.name == thatConnName)
                        {
                            if (thatConn.StartsWith(thisConnName))
                            {
                                continue;
                            }
                        }

                        newScore += c.potentialConnectionScores[conn][i] * tempMod;
                        tempMod *= NEXT_POTENTIAL_SCORE_MODIFIER;
                    }
                    newScore += CANT_CONNECT_SCORE_CAP * tempMod;

                    total += newScore - currScore;
                    //ExtraDebug("Scorediff for " + c.name + "(" + conn + ") == " + (total - origTotal));
                }
            }
            //ExtraDebug("Total scorediff for " + thisConnection + " == " + total);

            return total;
        }

        //returns a non-positive value... hopefully
        public float ScoreBonusWhenConnectionAdded(float score, string thisConn)
        {
            if (score >= CANT_CONNECT_SCORE_CAP)
                return 0;

            float total = 0;
            /*
            int idx = -1;
            for (int i = 0; i < this.potentialConnectionScores[thisConn].Count; i++) {
                if (this.potentialConnectionScores[thisConn][i] > score)
                {
                    idx = i;
                    break;
                }
            }
            if (idx >= 0)
                total += QuickPow(0.5f, idx) * (score - this.potentialConnectionScores[thisConn][idx]);
            else
                total += QuickPow(0.5f, idx) * (score - CANT_CONNECT_SCORE_CAP);
            */
            /*
            float tempMod = 1f;
            for (int i = 0; i < this.potentialConnectionScores[thisConn].Count; i++)
            {
                if (this.potentialConnectionScores[thisConn][i] > score)
                {
                    total += tempMod * (score - this.potentialConnectionScores[thisConn][i]);
                }
                tempMod *= 0.5f;
            }
            if (this.potentialConnectionScores[thisConn].Count < POTENTIAL_CONNECTIONS_CAP)
                total += tempMod * CANT_CONNECT_SCORE_CAP;
            */

            //current estimated score
            total -= this.estimatedConnectionScores[thisConn];

            //new estimated score
            bool lowerScoreUsed = false;
            float tempMod = 1f;
            for (int i = 0; i < this.potentialConnectionScores[thisConn].Count; i++)
            {
                float potenScore = this.potentialConnectionScores[thisConn][i];
                if (!lowerScoreUsed && score < potenScore)
                {
                    total += score * tempMod;
                    lowerScoreUsed = true;
                    i--; //repeat this connection score
                }
                else
                    total += potenScore * tempMod;
                tempMod *= NEXT_POTENTIAL_SCORE_MODIFIER;
            }
            total += tempMod * CANT_CONNECT_SCORE_CAP;

            //ExtraDebug("Scorebonus for " + this.name + "(" + thisConn + ") (new score=" + score + "): " + total);

            if (total > 0)
                return 0;
            return SCORE_POTENTIAL_MODIFIER * total;
        }

        public float TotalDistanceScore(List<Connectible> otherConnectibles)
        {
            float total = 0, origTotal = 0;
            foreach (Connectible c in otherConnectibles)
            {
                if (c.name != this.name)
                {
                    total += this.DistanceScore(c);// + this.OriginalDistanceScore(c);
                    origTotal += this.OriginalDistanceScore(c);
                }
            }
            //ExtraDebug(this.name + " dist score: " + total);
            return total * DISTANCE_SCORE_MODIFIER + origTotal * ORIGINAL_DISTANCE_SCORE_MODIFIER;
        }
        public float DistanceScore(Connectible c)
        {
            return Square(Square(this.radius + c.radius) / (this.position - c.position).SqrMagnitude());
        }
        //FALSE:encourages rooms to be located near rooms that they were previously near
        //actually: encourages rooms to be far from rooms that they were originally far from
        public float OriginalDistanceScore(Connectible c)
        {
            float origDist = (this.originalPosition - c.originalPosition).SqrMagnitude(),
                dist = (this.position - c.position).SqrMagnitude();
            return Square(origDist / dist) //avoids originally far connectibles
                - Mathf.Min(1f, Square(Square(this.radius + c.radius)) / (origDist * dist)); //encourages originally close connectibles
        }

        /** DEPRECATED
         * Returns the cheapest connection in connectible c, and that score
         */
        public KeyValuePair<string, float> LowestConnectionScore(Connectible c, string conn, bool allowPotentialBonus = false)
        {
            if (PROHIBIT_DOUBLE_CONNECTIONS && connections.Values.Contains(c.name))
                return new KeyValuePair<string, float>("", float.PositiveInfinity);

            float lowestScore = float.PositiveInfinity;
            string lowestConn = "";

            foreach (string thatConn in c.connections.Keys)
            {
                if (c.connections[thatConn] == "") //don't test already-made connections
                {
                    float score = ConnectionScore(c, conn, thatConn);
                    if (allowPotentialBonus)
                        score += c.ScoreBonusWhenConnectionAdded(score, thatConn);
                    if (score < lowestScore)
                    {
                        lowestScore = score;
                        lowestConn = thatConn;
                    }
                }
            }

            if (this.group != c.group)
                lowestScore += DIFF_GROUP_SCORE_PENALTY;

            return new KeyValuePair<string, float>(lowestConn, lowestScore);
        }
        public float EstimateNewConnectionScore(string thisConn, string skipName, List<Connectible> otherConnectibles)
        {
            //basically fully calculate all potential connections AND the estimated score
            //also add scoreBonus
            List<float> scores = new();

            //get potential scores
            foreach (Connectible c in otherConnectibles)
            {
                if (c.allConnectionsMade || c.name == this.name || c.name == skipName)
                    continue;

                foreach (string thatConn in c.connections.Keys)
                {
                    if (c.connections[thatConn] != "")
                        continue; //only test free connections

                    float connDist = this.ConnectionDistance(c, thisConn, thatConn);
                    if (connDist > POTENTIAL_CONN_DIST_LIMIT)
                        continue;
                    float score = this.ConnectionScore(c, thisConn, thatConn, connDist);
                    score += c.ScoreBonusWhenConnectionAdded(score, thatConn); //score bonus

                    if (scores.Count >= POTENTIAL_CONNECTIONS_CAP && score >= scores[POTENTIAL_CONNECTIONS_CAP - 1])
                        continue; //skip if cap reached AND score worse

                    //insert score
                    bool scoreAdded = false;
                    for (int i = 0; i < scores.Count; i++)
                    {
                        if (score < scores[i])
                        {
                            scores.Insert(i, score);
                            scoreAdded = true;
                            break;
                        }
                    }
                    if (!scoreAdded)
                        scores.Add(score);
                    if (scores.Count > POTENTIAL_CONNECTIONS_CAP)
                        scores.Pop();
                }
            }

            //get estimated score
            float estScore = 0;
            float tempMod = 1f;
            foreach (float score in scores)
            {
                estScore += score * tempMod;
                tempMod *= NEXT_POTENTIAL_SCORE_MODIFIER;
            }
            estScore += CANT_CONNECT_SCORE_CAP * tempMod;

            scores.Clear();

            //ExtraDebug("Estimated score for " + this.name + "(" + thisConn + "): " + estScore);

            return estScore;

        }


        public KeyValuePair<string, float> ResolveBestConnection(Connectible c, string thisConn, List<Connectible> otherConnectibles)
        {
            //ExtraDebug("TESTING CONNECTING " + c.name + " TO " + this.name + "(" + thisConn + ")");

            if (PROHIBIT_DOUBLE_CONNECTIONS && connections.Values.Contains(c.name))
                return new KeyValuePair<string, float>("", float.PositiveInfinity);

            float lowestScore = float.PositiveInfinity;
            float lowestActualScore = 0;
            string lowestConn = "";

            float scoreCap = (this.group == c.group) ? CANT_CONNECT_SCORE_CAP : CANT_CONNECT_SCORE_CAP + DIFF_GROUP_SCORE_PENALTY;

            foreach (string thatConn in c.connections.Keys)
            {
                //ExtraDebug("Testing " + c.name + "(" + thatConn + ")");
                if (c.connections[thatConn] == "") //don't test already-made connections
                {
                    c.SnapToConnection(this, thatConn, thisConn);
                    //ExtraDebug("New " + c.name + " pos: " + c.position.ToString() + "; (" + this.name + " pos: " + this.position.ToString() + ")");
                    float actualScore = ConnectionScore(c, thisConn, thatConn), score = actualScore;

                    //score += score - this.estimatedConnectionScores[thisConn]; //score improvement for thisConn

                    score += PrimaryAngleModifier(c, thisConn, thatConn);
                    //score += score - ((c.potentialConnectionScores[thatConn].Count > 0) ? c.potentialConnectionScores[thatConn][0] : CANT_CONNECT_SCORE_CAP);
                    //c is unconnected, so it shouldn't have any potential scores
                    //score += score - ((this.potentialConnectionScores[thisConn].Count > 0) ? this.potentialConnectionScores[thisConn][0] : CANT_CONNECT_SCORE_CAP);
                    score += c.TotalDistanceScore(otherConnectibles);

                    //score += ScoreDiffWhenConnectionTaken(this.name + ";" + thisConn, c.name + ";" + thatConn, otherConnectibles);

                    //add score for each not-connected connection
                    foreach (string thatConn2 in c.connections.Keys)
                    {
                        if (thatConn2 == thatConn)
                            continue;
                        /*
                        foreach (Connectible c2 in otherConnectibles)
                        {
                            if (c2.name == c.name || c2.name == this.name)
                                continue;
                            float score2 = c.LowestConnectionScore(c2, thatConn2, true).Value;

                            if (score2 > scoreCap)
                                score2 = scoreCap;
                            score += score2;
                        }
                        */
                        score += c.EstimateNewConnectionScore(thatConn2, this.name, otherConnectibles);
                    }

                    if (score < lowestScore)
                    {
                        lowestScore = score;
                        lowestActualScore = actualScore;
                        lowestConn = thatConn;
                    }
                }
            }

            //lowestScore += 0.25f; //this hopefully prevents negative scores from popping up

            //add scorediff for connection taken
            lowestScore += ScoreDiffWhenConnectionTaken(this.name + ";" + thisConn, this.name + ";" + thisConn, lowestActualScore, otherConnectibles);

            if (c.connections.Count < 2)
                lowestScore += SINGLE_CONNECTION_SCORE_PENALTY;

            if (this.group != c.group)
                lowestScore += DIFF_GROUP_SCORE_PENALTY;

            if (this.connections.Count == c.connections.Count)
                lowestScore += SAME_CONN_COUNT_PENALTY;

            //divide score for larger connectibles
            //lowestScore *= Mathf.Pow(CANT_CONNECT_SCORE_CAP, 1 - this.connections.Count);
            //hopefully prevents negative scores
            //lowestScore += CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP;
            //decrease score for larger connectibles?
            //lowestScore += CANT_CONNECT_SCORE_CAP * CANT_CONNECT_SCORE_CAP * (3f - 0.5f * c.connections.Count);

            return new KeyValuePair<string, float>(lowestConn, lowestScore);
        }

        public float ConnectionDistance(Connectible c, string thisConn, string thatConn)
        {
            return (this.WorldPosition(thisConn) - c.WorldPosition(thatConn)).SqrMagnitude() / Square(this.radius + c.radius);
        }

        public float ConnectionScore(Connectible c, string thisConn, string thatConn)
        {
            return ConnectionScore(c, thisConn, thatConn, ConnectionDistance(c, thisConn, thatConn));
        }
        public float ConnectionScore(Connectible c, string thisConn, string thatConn, float connDist)
        {
            //callsToConnectionScore++;
            //return (this.WorldPosition(conn1) - c.WorldPosition(conn2)).SqrMagnitude() + EST_DIST_SQR * (this.connLocations[conn1].normalized - c.connLocations[conn2].normalized).SqrMagnitude();
            //float connDist = (this.WorldPosition(thisConn) - c.WorldPosition(thatConn)).SqrMagnitude() / Square(this.radius + c.radius);
            return CONNECTION_DISTANCE_MODIFIER * connDist
                + QUAD_CONNECTION_DISTANCE_MODIFIER * connDist * connDist
                + ANGLE_SCORE_MODIFIER * (this.connLocationsNormalized[thisConn] + c.connLocationsNormalized[thatConn]).SqrMagnitude() //connLoc angle vs. connLoc angle
                + PLACEMENT_ANGLE_MODIFIER * ((this.connLocationsNormalized[thisConn] - (c.position - this.position).normalized).SqrMagnitude() //connLoc angle vs. position difference
                + (c.connLocationsNormalized[thatConn] - (this.position - c.position).normalized).SqrMagnitude()
                //+ (this.connLocationsNormalized[thisConn] - (c.WorldPosition(thatConn) - this.position).normalized).SqrMagnitude() //connLoc angle vs. actual angle (0 when snapped to connection)
                //+ (c.connLocationsNormalized[thatConn] - (this.WorldPosition(thisConn) - c.position).normalized).SqrMagnitude()
                + (this.connLocationsNormalized[thisConn] - (c.position - this.WorldPosition(thisConn)).normalized).SqrMagnitude() //connLoc angle vs. connLoc to c.position
                + (c.connLocationsNormalized[thatConn] - (this.position - c.WorldPosition(thatConn)).normalized).SqrMagnitude());
            //ExtraDebug(this.name + "(" + thisConn + ") to " + c.name + "(" + thatConn + ") score == " + score + "; (connDist=" + connDist + ")");
            //return score;
        }

        public float PrimaryAngleModifier(Connectible c, string thisConn, string thatConn)
        {
            return BONUS_ANGLE_PLACEMENT_MODIFIER * (this.connLocationsNormalized[thisConn] + c.connLocationsNormalized[thatConn]).SqrMagnitude();
        }
    }
    #endregion

    //get room positions
    #region Get_Room_Map_Positions
    public static Dictionary<string, Vector2> GetRoomMapPositions(string region, List<string> roomNames, string slugcat = "")
    {
        Dictionary<string, Vector2> dict = new();
        List<string> rooms = roomNames.ToArray().ToList();

        //look through map file and copy coordinates if present
        string mapPath = GetRegionMapFile(region, slugcat);

        if (!File.Exists(mapPath))
        {
            //if no map data, just randomize it!
            foreach (string room in rooms)
            {
                dict.Add(room, Custom.RNV() * UnityEngine.Random.value * 1000f);
            }
            return dict;
        }

        //read map data
        string[] mapLines = File.ReadAllLines(mapPath);

        for (int i = rooms.Count - 1; i >= 0; i--)
        {
            Vector2 pos = GetMapPositionOfRoom(mapLines, rooms[i]);
            if (!float.IsInfinity(pos.x))
            {
                dict.Add(rooms[i], pos);
                rooms.RemoveAt(i);
            }
        }

        //that should handle most rooms; certainly all vanilla ones
        //however, some modded rooms might not have map data in vanilla regions
        //so we will determine what mapped rooms the modded rooms connect to, and we will use the map locations of THESE rooms
        if (rooms.Count > 0)
        {
            //get connection data
            string worldPath = AssetManager.ResolveFilePath(string.Concat(new string[]
            {
                "World",
                Path.DirectorySeparatorChar.ToString(),
                region,
                Path.DirectorySeparatorChar.ToString(),
                "world_",
                region,
                ".txt"
            }));

            if (!File.Exists(worldPath))
            {
                //if no world data, just randomize it!
                foreach (string room in rooms)
                {
                    dict.Add(room, Custom.RNV() * UnityEngine.Random.value * 1000f);
                }
                return dict;
            }

            //read world data
            string[] worldLines = File.ReadAllLines(worldPath);

            roomsSearched.Clear();
            roomsSearched.Add("DISCONNECTED");
            for (int i = rooms.Count - 1; i >= 0; i--)
            {
                roomsSearched.Add(rooms[i]);

                //use a dedicated function to recursively search through rooms

                Vector2 pos = RecursivelySearchForMapPos(mapLines, worldLines, rooms[i]);
                if (!float.IsInfinity(pos.x))
                    dict.Add(rooms[i], pos);
                else
                    dict.Add(rooms[i], Custom.RNV() * UnityEngine.Random.value * 1000f);
                rooms.RemoveAt(i);
            }
            roomsSearched.Clear();
        }

        return dict;
    }

    private static Vector2 NULL_VECTOR2 = new Vector2(float.PositiveInfinity, float.PositiveInfinity);

    private static List<string> roomsSearched = new();
    private static Vector2 RecursivelySearchForMapPos(string[] mapLines, string[] worldLines, string room, int count = 1)
    {
        if (count > 10)
            return NULL_VECTOR2;

        //check if it's on the map
        Vector2 mapPos = GetMapPositionOfRoom(mapLines, room);
        if (!float.IsInfinity(mapPos.x))
        {
            return mapPos + Custom.RNV() * UnityEngine.Random.value * 50f * count;
        }

        roomsSearched.Add(room);

        List<string> conns = GetRoomConnections(worldLines, room, roomsSearched);
        foreach (string c in conns)
        {
            Vector2 pos = RecursivelySearchForMapPos(mapLines, worldLines, c, count + 1);
            if (!float.IsInfinity(pos.x))
            {
                if (count <= 1)
                    RegionRandomizer.LogSomething("Found gate through connections: " + room + ": " + pos.ToString());
                return pos;
            }
        }
        if (conns.Count < 1)
            RegionRandomizer.LogSomething("No unsearched room connections for " + room);

        return NULL_VECTOR2;
    }

    private static Vector2 GetMapPositionOfRoom(string[] lines, string room)
    {
        foreach (string line in lines)
        {
            if (line.StartsWith(room))
            {
                string l = line.Substring(line.IndexOf(':') + 2);
                string[] s = Regex.Split(l, "><");
                if (s.Length > 1)
                {
                    return new Vector2(float.Parse(s[0]), float.Parse(s[1]));
                }
            }
        }

        return NULL_VECTOR2;
    }

    private static List<string> GetRoomConnections(string[] lines, string room, List<string> excludeRooms = null)
    {
        string connectionData = "";
        bool foundRooms = false;
        foreach (string line in lines)
        {
            if (!foundRooms)
            {
                if (line.StartsWith("ROOMS"))
                    foundRooms = true;
            }
            else
            {
                if (line.StartsWith(room))
                {
                    connectionData = line;
                    break;
                }
                else if (line.StartsWith("END ROOMS"))
                    break;
            }
        }

        if (connectionData == "")
            return new List<string>();

        string[] sections = Regex.Split(connectionData, " : ");
        if (sections.Length < 2)
            return new List<string>();

        List<string> list = Regex.Split(sections[1], ", ").ToList();
        if (excludeRooms != null)
        {
            foreach (string r in excludeRooms)
                list.Remove(r);
        }

        return list;
    }

    public static string GetRegionMapFile(string region, string slugcat)
    {
        string mapPath = AssetManager.ResolveFilePath(string.Concat(new string[]
        {
            "World",
            Path.DirectorySeparatorChar.ToString(),
            region,
            Path.DirectorySeparatorChar.ToString(),
            "map_",
            region,
            "-",
            slugcat,
            ".txt"
        }));
        if (File.Exists(mapPath))
            return mapPath;

        return AssetManager.ResolveFilePath(string.Concat(new string[]
        {
            "World",
            Path.DirectorySeparatorChar.ToString(),
            region,
            Path.DirectorySeparatorChar.ToString(),
            "map_",
            region,
            ".txt"
        }));
    }
    #endregion

    private static float Square(float x)
    {
        return x * x;
    }
    private static float Cube(float x)
    {
        return x * x * x;
    }
    private static float QuickPow(float x, int pow)
    {
        float v = 1;
        for (int i = 0; i < pow; i++)
            v *= x;
        return v;
    }
    private static float Lerp(float t, float a, float b)
    {
        return a + (b - a) * t;
    }

    private static List<T> ShuffleList<T>(List<T> list)
    {
        List<T> newList = new();

        while (list.Count > 0)
        {
            int idx = UnityEngine.Random.Range(0, list.Count);
            newList.Add(list[idx]);
            list.RemoveAt(idx);
        }

        list.Clear();
        return newList;
    }
}
