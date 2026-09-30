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
                .font(.system(size: 12, weight: selectedPreset == title ? .bold : .regular))
                .lineLimit(1)
                .fixedSize(horizontal: true, vertical: false)
                .foregroundColor(selectedPreset == title ? .black : .white) // Black text if selected
                .padding(.vertical, 8)
                .frame(maxWidth: .infinity)
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
    @Binding var scrollIntensity: String
    @Binding var color1: Color
    @Binding var color2: Color
    @Binding var color3: Color
    var onClose: () -> Void
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
                HStack(spacing: 6) {
                    ForEach(["Chrome", "Sunset", "Midnight", "Custom"], id: \.self) { preset in
                        PresetButton(title: preset, selectedPreset: $meshPreset)
                    }
                }

                if meshPreset == "Custom" {
                    VStack(alignment: .leading, spacing: 14) {
                        ColorPicker("First glow", selection: $color1, supportsOpacity: false)
                        ColorPicker("Second glow", selection: $color2, supportsOpacity: false)
                        ColorPicker("Background", selection: $color3, supportsOpacity: false)
                        Text("Colors update instantly and save automatically.")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                    .foregroundStyle(.white)
                }

                VStack(alignment: .leading, spacing: 8) {
                    Text("Scroll intensity")
                        .font(.caption).foregroundStyle(.white.opacity(0.65))
                    Picker("Scroll intensity", selection: $scrollIntensity) {
                        ForEach(ScrollIntensity.allCases, id: \.rawValue) { intensity in
                            Text(intensity.rawValue).tag(intensity.rawValue)
                        }
                    }
                    .pickerStyle(.segmented)
                    .labelsHidden()
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
            .padding(24)
            .background(.ultraThinMaterial)
            .cornerRadius(20)
            .overlay(
                RoundedRectangle(cornerRadius: 20)
                    .stroke(Color.white.opacity(0.1), lineWidth: 1)
            )
            .shadow(radius: 20)
            .frame(maxWidth: 350)
        }
    }
}
