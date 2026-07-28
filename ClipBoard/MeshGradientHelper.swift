import SwiftUI

struct MeshGradientHelper {
    static func colors(for preset: String, custom: [Color]) -> [Color] {
        switch preset {
        case "Chrome":
            return [Color(white: 0.8), Color(white: 0.5), Color(white: 0.2)]
        case "Sunset":
            return [.orange, .pink, .purple]
        case "Midnight":
            // Deep Blues/Purples for the dark theme look
            return [Color(hex: "0f172a"), Color(hex: "1e1b4b"), Color(hex: "312e81")]
        case "Custom":
            return custom.isEmpty ? [.gray, .white, .black] : custom
        default:
            return [.gray, .black]
        }
    }
}

// Helper to allow Hex colors (e.g., "0f172a")
extension Color {
    init(hex: String) {
        let hex = hex.trimmingCharacters(in: CharacterSet.alphanumerics.inverted)
        var int: UInt64 = 0
        Scanner(string: hex).scanHexInt64(&int)
        let a, r, g, b: UInt64
        switch hex.count {
        case 3: // RGB (12-bit)
            (a, r, g, b) = (255, (int >> 8) * 17, (int >> 4 & 0xF) * 17, (int & 0xF) * 17)
        case 6: // RGB (24-bit)
            (a, r, g, b) = (255, int >> 16, int >> 8 & 0xFF, int & 0xFF)
        case 8: // ARGB (32-bit)
            (a, r, g, b) = (int >> 24, int >> 16 & 0xFF, int >> 8 & 0xFF, int & 0xFF)
        default:
            (a, r, g, b) = (1, 1, 1, 0)
        }

        self.init(
            .sRGB,
            red: Double(r) / 255,
            green: Double(g) / 255,
            blue:  Double(b) / 255,
            opacity: Double(a) / 255
        )
    }
}
