-- ============================================================================
-- HarvestCraft 2 Economy System Database Schema
-- ============================================================================

-- 데이터베이스 생성 (존재하지 않는 경우)
CREATE DATABASE IF NOT EXISTS harvestcraft2_economy 
CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

USE harvestcraft2_economy;

-- ============================================================================
-- 1. 서버 설정 테이블
-- ============================================================================
CREATE TABLE IF NOT EXISTS server_config (
    id INT AUTO_INCREMENT PRIMARY KEY,
    config_key VARCHAR(50) UNIQUE NOT NULL,
    config_value VARCHAR(200) NOT NULL,
    DESCRIPTION TEXT,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    INDEX idx_config_key (config_key)
) ENGINE=INNODB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================================
-- 2. 상점 아이템 마스터 테이블
-- ============================================================================
CREATE TABLE IF NOT EXISTS shop_items (
    id INT AUTO_INCREMENT PRIMARY KEY,
    item_id VARCHAR(100) UNIQUE NOT NULL,
    display_name VARCHAR(100) NOT NULL,
    category ENUM('Vanilla', 'FoodCore', 'Crops', 'FoodExtended', 'Tools') NOT NULL,
    
    -- 게임 정보
    hunger_restore INT DEFAULT 0,
    saturation_restore DECIMAL(4,1) DEFAULT 0.0,
    complexity_level ENUM('Low', 'Medium', 'High', 'Extreme') DEFAULT 'Low',
    
    -- 가격 정보
    base_sell_price DECIMAL(10,2) NOT NULL,
    base_buy_price DECIMAL(10,2),
    min_price DECIMAL(10,2) NOT NULL,
    max_price DECIMAL(10,2) NOT NULL,
    
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    INDEX idx_item_id (item_id),
    INDEX idx_category (category),
    INDEX idx_active (is_active),
    INDEX idx_item_lookup (item_id, is_active)
) ENGINE=INNODB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================================
-- 3. 거래 히스토리 테이블
-- ============================================================================
CREATE TABLE IF NOT EXISTS shop_transactions (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    player_id VARCHAR(36) NOT NULL,
    player_name VARCHAR(50) NOT NULL,
    item_id VARCHAR(100) NOT NULL,
    transaction_type ENUM('BuyFromNpc', 'SellToNpc') NOT NULL,
    quantity INT NOT NULL,
    unit_price DECIMAL(10,2) NOT NULL,
    total_amount DECIMAL(15,2) NOT NULL,
    
    -- 시장 상황 스냅샷
    demand_pressure DECIMAL(6,3) DEFAULT 0.000,
    supply_pressure DECIMAL(6,3) DEFAULT 0.000,
    online_players INT NOT NULL,
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    INDEX idx_player_time (player_id, created_at),
    INDEX idx_item_time (item_id, created_at),
    INDEX idx_type_time (transaction_type, created_at),
    INDEX idx_recent_trades (created_at DESC, item_id),
    
    FOREIGN KEY (item_id) REFERENCES shop_items(item_id) ON DELETE CASCADE
) ENGINE=INNODB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================================
-- 4. 가격 히스토리 테이블
-- ============================================================================
CREATE TABLE IF NOT EXISTS price_history (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    item_id VARCHAR(100) NOT NULL,
    price_timestamp TIMESTAMP NOT NULL,
    
    -- 가격 정보
    current_price DECIMAL(10,2) NOT NULL,
    base_price DECIMAL(10,2) NOT NULL,
    price_change_percent DECIMAL(5,2) NOT NULL,
    
    -- 시장 압력 정보
    demand_pressure DECIMAL(6,3) NOT NULL,
    supply_pressure DECIMAL(6,3) NOT NULL,
    net_pressure DECIMAL(6,3) NOT NULL,
    
    -- 거래량 정보
    period_buy_volume INT DEFAULT 0,
    period_sell_volume INT DEFAULT 0,
    weighted_buy_volume DECIMAL(8,1) DEFAULT 0.0,
    weighted_sell_volume DECIMAL(8,1) DEFAULT 0.0,
    
    -- 시스템 정보
    online_players INT NOT NULL,
    player_correction_factor DECIMAL(4,2) NOT NULL,
    
    INDEX idx_item_time (item_id, price_timestamp),
    INDEX idx_timestamp (price_timestamp),
    INDEX idx_time_item (price_timestamp DESC, item_id),
    
    FOREIGN KEY (item_id) REFERENCES shop_items(item_id) ON DELETE CASCADE
) ENGINE=INNODB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================================
-- 5. 플레이어 세션 테이블
-- ============================================================================
CREATE TABLE IF NOT EXISTS player_sessions (
    player_id VARCHAR(36) PRIMARY KEY,
    player_name VARCHAR(50) NOT NULL,
    login_time TIMESTAMP NOT NULL,
    last_activity TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    session_weight DECIMAL(3,1) DEFAULT 0.3,
    is_online BOOLEAN DEFAULT TRUE,
    
    INDEX idx_online_activity (is_online, last_activity),
    INDEX idx_weight_online (session_weight, is_online)
) ENGINE=INNODB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================================
-- 6. 초기 서버 설정 데이터 삽입
-- ============================================================================
INSERT INTO server_config (config_key, config_value, DESCRIPTION) VALUES
('base_online_players', '25', '기준 접속자 수 (서버 정원의 50%)'),
('price_update_interval', '600', '가격 업데이트 주기 (초)'),
('max_price_change', '0.10', '주기당 최대 가격 변동률'),
('min_price_ratio', '0.50', '기본가 대비 최저가 비율'),
('max_price_ratio', '3.00', '기본가 대비 최고가 비율'),
('session_weight_instant', '0.3', '즉석 접속자 가중치 (10분 미만)'),
('session_weight_short', '0.6', '단기 접속자 가중치 (10-30분)'),
('session_weight_medium', '0.8', '중기 접속자 가중치 (30분-2시간)'),
('session_weight_long', '1.0', '장기 접속자 가중치 (2시간+)')
ON DUPLICATE KEY UPDATE 
    config_value = VALUES(config_value),
    DESCRIPTION = VALUES(DESCRIPTION);

-- ============================================================================
-- 7. 바닐라 테스트 아이템 데이터
-- ============================================================================
INSERT INTO shop_items (item_id, display_name, category, hunger_restore, saturation_restore, complexity_level, base_sell_price, base_buy_price, min_price, max_price) VALUES
-- 기본 농작물
('minecraft:wheat', '밀', 'Vanilla', 0, 0.0, 'Low', 2.00, 1.50, 1.00, 6.00),
('minecraft:carrot', '당근', 'Vanilla', 3, 3.6, 'Low', 3.00, 2.25, 1.50, 9.00),
('minecraft:potato', '감자', 'Vanilla', 1, 0.6, 'Low', 2.50, 1.88, 1.25, 7.50),
('minecraft:beetroot', '비트', 'Vanilla', 1, 1.2, 'Low', 2.80, 2.10, 1.40, 8.40),

-- 기본 요리
('minecraft:bread', '빵', 'Vanilla', 5, 6.0, 'Low', 8.00, 6.00, 4.00, 24.00),
('minecraft:baked_potato', '구운 감자', 'Vanilla', 5, 6.0, 'Low', 7.50, 5.63, 3.75, 22.50),
('minecraft:beetroot_soup', '비트 수프', 'Vanilla', 6, 7.2, 'Medium', 15.00, 11.25, 7.50, 45.00),
('minecraft:mushroom_stew', '버섯 스튜', 'Vanilla', 6, 7.2, 'Medium', 18.00, 13.50, 9.00, 54.00),

-- 육류 및 고급 요리
('minecraft:cooked_beef', '구운 소고기', 'Vanilla', 8, 12.8, 'Low', 12.00, 9.00, 6.00, 36.00),
('minecraft:cooked_porkchop', '구운 돼지고기', 'Vanilla', 8, 12.8, 'Low', 12.00, 9.00, 6.00, 36.00),
('minecraft:cooked_chicken', '구운 닭고기', 'Vanilla', 6, 7.2, 'Low', 10.00, 7.50, 5.00, 30.00),
('minecraft:cooked_salmon', '구운 연어', 'Vanilla', 6, 9.6, 'Low', 11.00, 8.25, 5.50, 33.00),

-- 과일류
('minecraft:apple', '사과', 'Vanilla', 4, 2.4, 'Low', 4.00, 3.00, 2.00, 12.00),
('minecraft:golden_apple', '황금 사과', 'Vanilla', 4, 9.6, 'High', 50.00, 37.50, 25.00, 150.00),

-- 기타 재료
('minecraft:sugar', '설탕', 'Vanilla', 0, 0.0, 'Low', 1.50, 1.13, 0.75, 4.50),
('minecraft:egg', '달걀', 'Vanilla', 0, 0.0, 'Low', 3.50, 2.63, 1.75, 10.50),
('minecraft:milk_bucket', '우유 양동이', 'Vanilla', 0, 0.0, 'Low', 8.00, 6.00, 4.00, 24.00)

ON DUPLICATE KEY UPDATE 
    display_name = VALUES(display_name),
    hunger_restore = VALUES(hunger_restore),
    saturation_restore = VALUES(saturation_restore),
    base_sell_price = VALUES(base_sell_price),
    base_buy_price = VALUES(base_buy_price),
    min_price = VALUES(min_price),
    max_price = VALUES(max_price),
    updated_at = CURRENT_TIMESTAMP;

-- ============================================================================
-- 8. 성능 최적화를 위한 추가 인덱스
-- ============================================================================

-- 거래 테이블 최적화
ALTER TABLE shop_transactions 
ADD INDEX idx_item_type_time (item_id, transaction_type, created_at DESC);

-- 가격 히스토리 최적화
ALTER TABLE price_history 
ADD INDEX idx_item_timestamp_desc (item_id, price_timestamp DESC);

-- 복합 인덱스 추가
ALTER TABLE shop_transactions 
ADD INDEX idx_player_item_time (player_id, item_id, created_at DESC);

-- ============================================================================
-- 9. 테이블 통계 업데이트 및 최적화
-- ============================================================================
ANALYZE TABLE server_config;
ANALYZE TABLE shop_items;
ANALYZE TABLE shop_transactions;
ANALYZE TABLE price_history;
ANALYZE TABLE player_sessions;

-- ============================================================================
-- 완료 메시지
-- ============================================================================
SELECT 'HarvestCraft 2 Economy Database 초기화 완료!' AS message;
SELECT 
    (SELECT COUNT(*) FROM shop_items) AS total_items,
    (SELECT COUNT(*) FROM shop_items WHERE is_active = TRUE) AS active_items,
    (SELECT COUNT(*) FROM server_config) AS config_count;