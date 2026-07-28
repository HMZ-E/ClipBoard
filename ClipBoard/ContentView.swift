import SwiftUI

struct ContentView: View {
    @StateObject private var manager = ClipboardManager()
    @State private var searchText = ""
    @State private var showSettings = false
    @AppStorage("meshPreset") private var meshPreset = "Chrome"
    @State private var customColors: [Color] = Array(repeating: .white, count: 9)

    var filteredItems: [ClipboardItem] {
        if searchText.isEmpty { return manager.items }
        return manager.items.filter {
            if case .text(let str) = $0.content {
                return str.localizedCaseInsensitiveContains(searchText)
            }
            return false
        }
    }

    var body: some View {
        ZStack {
            // 1. Animated Background
            AnimatedMeshView(colors: MeshGradientHelper.colors(for: meshPreset, custom: customColors))
            
            // 2. Dark Overlay (To match the "Midnight" screenshot look)
            Rectangle()
                .fill(.black.opacity(0.4))
                .background(.ultraThinMaterial)
                .ignoresSafeArea()

            VStack(spacing: 0) {
                // Header
                HStack {
                    if let _ = NSImage(named: "ClipboardLogo") {
                        Image("ClipboardLogo")
                            .resizable()
                            .scaledToFit()
                            .frame(height: 35)
                    } else {
                        Text("CLIPBOARD")
                            .font(.system(size: 24, weight: .heavy, design: .rounded))
                            .foregroundStyle(.white)
                    }
                    
                    Spacer()
                    
                    Button(action: { showSettings = true }) {
                        Image(systemName: "slider.horizontal.3")
                            .font(.system(size: 18))
                            .foregroundColor(.white.opacity(0.7))
                    }
                    .buttonStyle(.plain)
                }
                .padding(.horizontal, 20)
                .padding(.top, 20)
                .padding(.bottom, 10)

                // Search Bar
                HStack {
                    Image(systemName: "magnifyingglass")
                        .foregroundColor(.gray)
                    TextField("Search...", text: $searchText)
                        .textFieldStyle(.plain)
                        .foregroundColor(.white)
                }
                .padding(10)
                .background(Color.black.opacity(0.3))
                .cornerRadius(10)
                .overlay(
                    RoundedRectangle(cornerRadius: 10)
                        .stroke(Color.white.opacity(0.1), lineWidth: 1)
                )
                .padding(.horizontal, 20)
                .padding(.bottom, 15)

                // List
                ScrollView {
                    LazyVStack(spacing: 12) {
                        ForEach(filteredItems) { item in
                            ClipboardRowView(item: item)
                        }
                    }
                    .padding(.horizontal, 20)
                    .padding(.bottom, 20)
                }
            }
        }
        .frame(width: 380, height: 600)
        .overlay(
            Group {
                if showSettings {
                    SettingsView(
                        meshPreset: $meshPreset,
                        customColors: $customColors,
                        onClose: { showSettings = false },
                        saveCustomColors: {}
                    )
                    .transition(.opacity.combined(with: .scale(scale: 0.95)))
                }
            }
        )
        .animation(.easeInOut(duration: 0.2), value: showSettings)
    }
}
