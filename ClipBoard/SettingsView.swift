import SwiftUI

struct PresetButton: View {
    let title: String
    @Binding var selectedPreset: String
    @State private var isHovered = false

    var body: some View {
        Button(action: {
            selectedPreset = title
        }) {
            Text(title)
                .fontWeight(selectedPreset == title ? .bold : .regular)
                .foregroundColor(selectedPreset == title ? .black : .white) // Black text if selected
                .padding(.vertical, 8)
                .padding(.horizontal, 16)
                .background(selectedPreset == title ? Color.white : Color.black.opacity(0.3))
                .cornerRadius(20)
                .overlay(
                    RoundedRectangle(cornerRadius: 20)
                        .stroke(Color.white.opacity(0.2), lineWidth: 1)
                )
        }
        .buttonStyle(.plain)
        .scaleEffect(isHovered ? 1.05 : 1.0)
        .onHover { hovering in
            withAnimation(.easeInOut(duration: 0.2)) {
                isHovered = hovering
            }
        }
    }
}

struct SettingsView: View {
    @Binding var meshPreset: String
    @Binding var customColors: [Color]
    var onClose: () -> Void
    var saveCustomColors: () -> Void
    @State private var isCloseHovered = false

    var body: some View {
        ZStack {
            Color.black.opacity(0.8).ignoresSafeArea() // Dark background for modal

            VStack(spacing: 25) {
                
                // Header Image or Text
                if let _ = NSImage(named: "ClipboardLogo") {
                    Image("ClipboardLogo")
                        .resizable()
                        .scaledToFit()
                        .frame(height: 50)
                } else {
                    Text("Settings")
                        .font(.title)
                        .bold()
                }

                Text("Mood")
                    .font(.headline)
                    .foregroundColor(.gray)

                // Mood Buttons
                HStack(spacing: 12) {
                    ForEach(["Chrome", "Sunset", "Midnight", "Custom"], id: \.self) { preset in
                        PresetButton(title: preset, selectedPreset: $meshPreset)
                    }
                }

                Divider().background(Color.white.opacity(0.2))

                Button("Close") {
                    onClose()
                }
                .foregroundColor(.white)
                .padding(.vertical, 8)
                .padding(.horizontal, 20)
                .background(Color.white.opacity(0.1))
                .cornerRadius(12)
                .buttonStyle(.plain)
                .onHover { isCloseHovered = $0 }
                .scaleEffect(isCloseHovered ? 1.05 : 1.0)
            }
            .padding(30)
            .background(.ultraThinMaterial)
            .cornerRadius(20)
            .overlay(
                RoundedRectangle(cornerRadius: 20)
                    .stroke(Color.white.opacity(0.1), lineWidth: 1)
            )
            .shadow(radius: 20)
            .frame(maxWidth: 400)
        }
    }
}
