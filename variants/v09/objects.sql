CREATE OR REPLACE VIEW v_product_cost AS SELECT p.id,p.name,COALESCE(m.material_cost,0) material_cost,COALESCE(o.operation_cost,0) operation_cost FROM products p LEFT JOIN (SELECT n.product_id,SUM(n.norm*m.price) material_cost FROM material_norms n JOIN materials m ON m.id=n.material_id GROUP BY n.product_id) m ON m.product_id=p.id LEFT JOIN (SELECT n.product_id,SUM(n.hours*o.rate_hour) operation_cost FROM operation_norms n JOIN operations o ON o.id=n.operation_id GROUP BY n.product_id) o ON o.product_id=p.id;
CREATE OR REPLACE VIEW v_order_cost AS SELECT o.id,c.name customer,o.status,ROUND(COALESCE(SUM(l.quantity*(p.material_cost+p.operation_cost)),0),2) total FROM orders o JOIN customers c ON c.id=o.customer_id LEFT JOIN order_lines l ON l.order_id=o.id LEFT JOIN v_product_cost p ON p.id=l.product_id GROUP BY o.id,c.name,o.status;
DROP PROCEDURE IF EXISTS sp_create_order;
DELIMITER $$
CREATE PROCEDURE sp_create_order(IN customer VARCHAR(32),IN actor INT,IN product INT,IN qty INT)
BEGIN
 DECLARE oid INT;
 DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
 IF qty<=0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Количество должно быть положительным'; END IF;
 START TRANSACTION;
 INSERT INTO orders(customer_id,created_by) VALUES(customer,actor);
 SET oid=LAST_INSERT_ID();
 INSERT INTO order_lines(order_id,product_id,quantity) VALUES(oid,product,qty);
 COMMIT;
 SELECT oid id;
END$$
DELIMITER ;
DROP PROCEDURE IF EXISTS sp_change_status;
DELIMITER $$
CREATE PROCEDURE sp_change_status(IN oid INT,IN new_status VARCHAR(30))
BEGIN
 IF new_status='В производстве' THEN
  INSERT IGNORE INTO production_orders(order_id,launch_date,department) VALUES(oid,CURRENT_DATE,'Основное подразделение');
 END IF;
 UPDATE orders SET status=new_status WHERE id=oid;
END$$
DELIMITER ;
DROP PROCEDURE IF EXISTS sp_unblock;
DELIMITER $$
CREATE PROCEDURE sp_unblock(IN uid INT)
BEGIN
 UPDATE users SET blocked=FALSE,failed_attempts=0 WHERE id=uid;
END$$
DELIMITER ;
DROP TRIGGER IF EXISTS tr_order_created;
CREATE TRIGGER tr_order_created AFTER INSERT ON orders FOR EACH ROW INSERT INTO order_audit(order_id,new_status) VALUES(NEW.id,NEW.status);
DROP TRIGGER IF EXISTS tr_order_status;
DELIMITER $$
CREATE TRIGGER tr_order_status AFTER UPDATE ON orders FOR EACH ROW
BEGIN
 IF OLD.status<>NEW.status THEN INSERT INTO order_audit(order_id,old_status,new_status) VALUES(NEW.id,OLD.status,NEW.status); END IF;
END$$
DELIMITER ;
