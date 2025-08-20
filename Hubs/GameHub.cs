using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Linq;

namespace Web_chơi_cờ_Caro.Hubs
{
    public class GameHub : Hub
    {
        // Lưu trữ thông tin phòng và người chơi
        private static readonly ConcurrentDictionary<string, RoomInfo> _rooms = new();
        private static readonly ConcurrentDictionary<string, string> _userRooms = new(); // connectionId -> roomId

        public class RoomInfo
        {
            public string RoomId { get; set; } = "";
            public string RoomName { get; set; } = "";
            public string HostId { get; set; } = "";
            public string HostName { get; set; } = "";
            public List<PlayerInfo> Players { get; set; } = new();
            public GameState GameState { get; set; } = new();
            public int MaxPlayers { get; set; } = 2;
            public bool IsPrivate { get; set; } = false;
            public DateTime CreatedAt { get; set; } = DateTime.Now;
        }

        public class PlayerInfo
        {
            public string ConnectionId { get; set; } = "";
            public string Name { get; set; } = "";
            public string Symbol { get; set; } = ""; // X hoặc O
            public bool IsHost { get; set; } = false;
        }

        public class GameState
        {
            public bool IsStarted { get; set; } = false;
            public string CurrentPlayer { get; set; } = "X";
            public int[][] Board { get; set; } = CreateEmptyBoard(); // 0 = empty, 1 = X, 2 = O
            public int TimeLeft { get; set; } = 60;
            public string Winner { get; set; } = "";

            private static int[][] CreateEmptyBoard()
            {
                var board = new int[20][];
                for (int i = 0; i < 20; i++)
                {
                    board[i] = new int[20];
                }
                return board;
            }
        }

        // Test connection method (used by client to verify hub methods)
        public async Task TestConnection(string roomId)
        {
            var connectionId = Context.ConnectionId;
            var player = _rooms.TryGetValue(roomId, out var room)
                ? room.Players.FirstOrDefault(p => p.ConnectionId == connectionId)
                : null;

            await Clients.Caller.SendAsync(
                "TestConnectionResult",
                $"Connection ID: {connectionId}",
                $"Room ID: {roomId}",
                $"Player: {player?.Name ?? "Unknown"}",
                $"Is Host: {player?.IsHost ?? false}"
            );
        }


        // Kiểm tra phòng có tồn tại không
        public async Task CheckRoomExists(string roomId)
        {
            var exists = _rooms.TryGetValue(roomId, out var room);
            var playerCount = exists ? room.Players.Count : 0;
            var maxPlayers = exists ? room.MaxPlayers : 2;
            
            await Clients.Caller.SendAsync("RoomExistsResult", roomId, exists, playerCount, maxPlayers);
        }

        // Tham gia phòng
        public async Task JoinRoom(string roomId, string playerName)
        {
            var connectionId = Context.ConnectionId;
            
            Console.WriteLine($"=== JOIN ROOM REQUEST ===");
            Console.WriteLine($"Room ID: {roomId}");
            Console.WriteLine($"Player Name: {playerName}");
            Console.WriteLine($"Connection ID: {connectionId}");
            
            // Kiểm tra xem người chơi đã ở phòng nào chưa
            if (_userRooms.TryGetValue(connectionId, out var currentRoom))
            {
                Console.WriteLine($"Player already in room {currentRoom}, leaving first...");
                await LeaveRoom(currentRoom);
            }

            // Tạo phòng mới nếu chưa tồn tại
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                room = new RoomInfo
                {
                    RoomId = roomId,
                    RoomName = $"Phòng {roomId}",
                    HostId = connectionId,
                    HostName = playerName,
                    MaxPlayers = 2
                };
                _rooms.TryAdd(roomId, room);
                Console.WriteLine($"Created new room: {roomId} by {playerName}");
            }
            else
            {
                Console.WriteLine($"Joined existing room: {roomId} by {playerName}");
                Console.WriteLine($"Current players in room: {room.Players.Count}");
            }

            // Kiểm tra xem phòng có đầy không
            if (room.Players.Count >= room.MaxPlayers)
            {
                Console.WriteLine($"Room {roomId} is full!");
                await Clients.Caller.SendAsync("RoomFull", roomId);
                return;
            }

            // Thêm người chơi vào phòng
            var player = new PlayerInfo
            {
                ConnectionId = connectionId,
                Name = playerName,
                Symbol = room.Players.Count == 0 ? "X" : "O",
                IsHost = room.Players.Count == 0
            };

            room.Players.Add(player);
            _userRooms.TryAdd(connectionId, roomId);

            Console.WriteLine($"Added player {playerName} to room {roomId}");
            Console.WriteLine($"Total rooms: {_rooms.Count}");
            Console.WriteLine($"Total players in room {roomId}: {room.Players.Count}");
            Console.WriteLine($"Player is host: {player.IsHost}");
            Console.WriteLine($"Player symbol: {player.Symbol}");

            // Tham gia group SignalR
            await Groups.AddToGroupAsync(connectionId, roomId);
            Console.WriteLine($"Added {playerName} to SignalR group {roomId}");

            // Thông báo cho tất cả người chơi trong phòng
            await Clients.Group(roomId).SendAsync("PlayerJoined", player.Name, player.Symbol, room.Players.Count);
            Console.WriteLine($"Sent PlayerJoined event to group {roomId}");
            
            // Gửi lại danh sách người chơi cập nhật cho tất cả trong phòng
            await Clients.Group(roomId).SendAsync("PlayerListUpdated", room.Players);
            Console.WriteLine($"Sent PlayerListUpdated event to group {roomId}");

            // Gửi thông tin phòng cho người chơi mới
            await Clients.Caller.SendAsync("RoomJoined", room.RoomId, room.RoomName, room.Players, room.GameState);
            Console.WriteLine($"Sent RoomJoined event to {playerName}");
            Console.WriteLine($"Room players: {string.Join(", ", room.Players.Select(p => $"{p.Name}({p.Symbol})"))}");

            // Cập nhật danh sách phòng cho tất cả
            await BroadcastRoomList();
            Console.WriteLine($"=== JOIN ROOM COMPLETED ===");
        }

        // Rời phòng
        public async Task LeaveRoom(string roomId)
        {
            var connectionId = Context.ConnectionId;
            
            if (_rooms.TryGetValue(roomId, out var room))
            {
                var player = room.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
                if (player != null)
                {
                    room.Players.Remove(player);
                    
                    // Nếu chủ phòng rời đi, chuyển quyền cho người chơi khác
                    if (player.IsHost && room.Players.Count > 0)
                    {
                        var newHost = room.Players.First();
                        newHost.IsHost = true;
                        room.HostId = newHost.ConnectionId;
                        room.HostName = newHost.Name;
                    }

                    // Nếu không còn ai trong phòng, xóa phòng
                    if (room.Players.Count == 0)
                    {
                        _rooms.TryRemove(roomId, out _);
                    }
                    else
                    {
                        // Thông báo cho người chơi khác
                        await Clients.Group(roomId).SendAsync("PlayerLeft", player.Name, room.Players.Count);
                        
                        // Gửi lại danh sách người chơi cập nhật cho tất cả trong phòng
                        await Clients.Group(roomId).SendAsync("PlayerListUpdated", room.Players);
                        Console.WriteLine($"Sent PlayerListUpdated event after player left to group {roomId}");
                    }
                }
            }

            _userRooms.TryRemove(connectionId, out _);
            await Groups.RemoveFromGroupAsync(connectionId, roomId);
            
            // Cập nhật danh sách phòng
            await BroadcastRoomList();
        }

        // Bắt đầu game
        public async Task StartGame(string roomId)
        {
            Console.WriteLine($"=== START GAME REQUEST ===");
            Console.WriteLine($"Room ID: {roomId}");
            Console.WriteLine($"Connection ID: {Context.ConnectionId}");
            
            if (_rooms.TryGetValue(roomId, out var room))
            {
                Console.WriteLine($"Room found, players count: {room.Players.Count}");
                Console.WriteLine($"Room players: {string.Join(", ", room.Players.Select(p => $"{p.Name}({p.Symbol})"))}");
                
                var player = room.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
                Console.WriteLine($"Player found: {player?.Name}, IsHost: {player?.IsHost}");
                
                if (player?.IsHost == true)
                {
                    Console.WriteLine("Starting game...");
                    room.GameState.IsStarted = true;
                    room.GameState.CurrentPlayer = "X";
                    room.GameState.TimeLeft = 60;
                    room.GameState.Winner = "";

                    await Clients.Group(roomId).SendAsync("GameStarted", room.GameState);
                    Console.WriteLine("Game started successfully");
                    Console.WriteLine($"Sent GameStarted event to group {roomId}");
                }
                else
                {
                    Console.WriteLine("Player is not host, cannot start game");
                    await Clients.Caller.SendAsync("Error", "Chỉ chủ phòng mới có thể bắt đầu game!");
                }
            }
            else
            {
                Console.WriteLine($"Room {roomId} not found");
                await Clients.Caller.SendAsync("Error", "Không tìm thấy phòng!");
            }
            
            Console.WriteLine($"=== START GAME COMPLETED ===");
        }

        // Thực hiện nước đi
        public async Task MakeMove(string roomId, int row, int col)
        {
            Console.WriteLine($"=== MAKE MOVE REQUEST ===");
            Console.WriteLine($"Room ID: {roomId}");
            Console.WriteLine($"Row: {row}, Col: {col}");
            Console.WriteLine($"Connection ID: {Context.ConnectionId}");
            
            if (_rooms.TryGetValue(roomId, out var room))
            {
                Console.WriteLine($"Room found, game started: {room.GameState.IsStarted}");
                Console.WriteLine($"Current player: {room.GameState.CurrentPlayer}");
                Console.WriteLine($"Winner: {room.GameState.Winner}");
                
                var player = room.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
                Console.WriteLine($"Player found: {player?.Name}, Symbol: {player?.Symbol}");
                
                if (player != null && room.GameState.IsStarted && !room.GameState.Winner.Any())
                {
                    // Kiểm tra lượt đi
                    if (room.GameState.CurrentPlayer == player.Symbol)
                    {
                        Console.WriteLine($"Player {player.Name} can make move");

                        // Kiểm tra ô có trống không
                        if (row >= 0 && row < 20 && col >= 0 && col < 20 && room.GameState.Board[row][col] == 0)
                        {
                            Console.WriteLine($"Position ({row}, {col}) is empty, making move");
                            
                            // Đặt quân cờ
                            room.GameState.Board[row][col] = player.Symbol == "X" ? 1 : 2;
                            
                            // Kiểm tra thắng
                            if (CheckWin(room.GameState.Board, row, col, room.GameState.Board[row][col]))
                            {
                                room.GameState.Winner = player.Symbol;
                                Console.WriteLine($"Player {player.Name} wins!");
                                await Clients.Group(roomId).SendAsync("GameEnded", player.Symbol, room.GameState);
                                return;
                            }

                            // Kiểm tra hòa
                            if (IsBoardFull(room.GameState.Board))
                            {
                                room.GameState.Winner = "Draw";
                                Console.WriteLine("Game is a draw!");
                                await Clients.Group(roomId).SendAsync("GameEnded", "Draw", room.GameState);
                                return;
                            }

                            // Chuyển lượt
                            room.GameState.CurrentPlayer = room.GameState.CurrentPlayer == "X" ? "O" : "X";
                            room.GameState.TimeLeft = 60;

                            // Gửi thông tin nước đi cho tất cả
                            await Clients.Group(roomId).SendAsync("MoveMade", row, col, player.Symbol, room.GameState);
                            Console.WriteLine($"Sent MoveMade event to group {roomId}");
                        }
                        else
                        {
                            Console.WriteLine($"Position ({row}, {col}) is invalid or occupied");
                            await Clients.Caller.SendAsync("Error", "Nước đi không hợp lệ!");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Not player's turn. Current: {room.GameState.CurrentPlayer}, Player: {player.Symbol}");
                        await Clients.Caller.SendAsync("Error", "Chưa đến lượt của bạn!");
                    }
                }
                else
                {
                    Console.WriteLine($"Cannot make move. Player: {player?.Name}, Game started: {room.GameState.IsStarted}, Winner: {room.GameState.Winner}");
                    await Clients.Caller.SendAsync("Error", "Không thể thực hiện nước đi!");
                }
            }
            else
            {
                Console.WriteLine($"Room {roomId} not found");
                await Clients.Caller.SendAsync("Error", "Không tìm thấy phòng!");
            }
            
            Console.WriteLine($"=== MAKE MOVE COMPLETED ===");
        }

        // Reset game
        public async Task ResetGame(string roomId)
        {
            if (_rooms.TryGetValue(roomId, out var room))
            {
                var player = room.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
                if (player?.IsHost == true)
                {
                    room.GameState = new GameState();
                    await Clients.Group(roomId).SendAsync("GameReset", room.GameState);
                }
            }
        }

        // Gửi tin nhắn chat
        public async Task SendMessage(string roomId, string message)
        {
            var player = _rooms.TryGetValue(roomId, out var room) 
                ? room.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId) 
                : null;

            if (player != null)
            {
                await Clients.Group(roomId).SendAsync("MessageReceived", player.Name, message);
            }
        }

        // Lấy danh sách phòng
        public async Task GetRoomList()
        {
            var roomList = _rooms.Values.Select(r => new
            {
                r.RoomId,
                r.RoomName,
                r.HostName,
                Players = r.Players.Count,
                MaxPlayers = r.MaxPlayers,
                r.IsPrivate,
                Status = r.GameState.IsStarted ? "playing" : "waiting"
            }).ToList();

            await Clients.Caller.SendAsync("RoomListUpdated", roomList);
        }

        // Broadcast danh sách phòng
        private async Task BroadcastRoomList()
        {
            var roomList = _rooms.Values.Select(r => new
            {
                r.RoomId,
                r.RoomName,
                r.HostName,
                Players = r.Players.Count,
                MaxPlayers = r.MaxPlayers,
                r.IsPrivate,
                Status = r.GameState.IsStarted ? "playing" : "waiting"
            }).ToList();

            await Clients.All.SendAsync("RoomListUpdated", roomList);
        }

        // Kiểm tra thắng
                // Kiểm tra thắng
        private bool CheckWin(int[][] board, int row, int col, int player)
        {
            int rows = board.Length;
            int cols = board[0].Length;
            int count;

            // Kiểm tra hàng ngang
            count = 1;
            for (int i = col - 1; i >= 0 && board[row][i] == player; i--) count++;
            for (int i = col + 1; i < cols && board[row][i] == player; i++) count++;
            if (count >= 5) return true;

            // Kiểm tra hàng dọc
            count = 1;
            for (int i = row - 1; i >= 0 && board[i][col] == player; i--) count++;
            for (int i = row + 1; i < rows && board[i][col] == player; i++) count++;
            if (count >= 5) return true;

            // Kiểm tra đường chéo chính
            count = 1;
            for (int i = 1; row - i >= 0 && col - i >= 0 && board[row - i][col - i] == player; i++) count++;
            for (int i = 1; row + i < rows && col + i < cols && board[row + i][col + i] == player; i++) count++;
            if (count >= 5) return true;

            // Kiểm tra đường chéo phụ
            count = 1;
            for (int i = 1; row - i >= 0 && col + i < cols && board[row - i][col + i] == player; i++) count++;
            for (int i = 1; row + i < rows && col - i >= 0 && board[row + i][col - i] == player; i++) count++;
            if (count >= 5) return true;

            return false;
        }

               // Kiểm tra bàn cờ đầy
        private bool IsBoardFull(int[][] board)
        {
            int rows = board.Length;
            int cols = board[0].Length;
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    if (board[i][j] == 0) return false;
                }
            }
            return true;
        }
        // Khi người chơi disconnect
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            
            if (_userRooms.TryGetValue(connectionId, out var roomId))
            {
                await LeaveRoom(roomId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
} 